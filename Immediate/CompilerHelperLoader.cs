using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Samples.Debugging.CorDebug;

namespace Immediate
{
    public class CompilerHelperLoader
    {
        public readonly TaskCompletionSourceResettable taskCompletionSourceWrapper;
        public const int AssemblyLoadMethodToken = 100680300;
        public const int ArrayCreateInstanceMethodToken = 100663950;
        public const int GetTypeMethodToken = 100668401;
        public const int IntParseMethodToken = 100667218;
        public static int CompilerHelperAddMethodToken { get; private set; }
        public static int CompilerHelperGetMethodToken { get; private set; }
        public static int CompilerHelperTestMethodMethodToken { get; private set; }
        public static readonly int PointerSize = sizeof(int);
        public const string DebuggerAssemblyName = "debuggerHelpersAssembly";

        public const string Code = @"
using System.Collections.Generic;
using System;
namespace Immediate
{
    public static class CompilerHelper
    {
        public static Dictionary<string, object> PseudoVariables = new Dictionary<string, object>();

        public static object Get(string name)
        {
            Console.WriteLine($""Get method: Name {name}"");
            return PseudoVariables[name];
        }

        public static void TestMethod(){}

        public static void Add(string name, object value)
        {
            Console.WriteLine($""Add method: Name {name}, value {value}"");
            PseudoVariables.Add(name, value);
        }
    }
}   
";

        public CompilerHelperLoader(TaskCompletionSourceResettable taskCompletionSourceResettable)
        {
            taskCompletionSourceWrapper = taskCompletionSourceResettable;
        }

        public async Task LoadCompilerHelper(CorThreadEventArgs args)
        {
            var (pe, pdb) = GetRoslynStreams(args);

            var peArray = await LoadArrayIntoProcess(pe.ToArray(), args);
            var pdbArray = await LoadArrayIntoProcess(pdb.ToArray().ToArray(), args);
            var result = await LoadAssemblyIntoProcess(peArray, pdbArray, args);
        }

        private (MemoryStream per, MemoryStream pdb) GetRoslynStreams(CorThreadEventArgs args)
        {
            var metadata = args.AppDomain.Assemblies.Cast<CorAssembly>()
                .SelectMany(x => x.Modules.Cast<DynamicCorModule>())
                .Select(x => MetadataReference.CreateFromFile(x.Name))
                .ToArray();

            CSharpCompilation t = CSharpCompilation.Create(DebuggerAssemblyName)
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .WithReferences(metadata)
                .AddSyntaxTrees(SyntaxFactory.ParseSyntaxTree(Code));


            var pe = new MemoryStream();
            var pdb = new MemoryStream();
            var result = t.Emit(pe, pdb);
            if (result.Success == false) throw new Exception();
            File.WriteAllBytes("dynamicLibrary.dll", pe.ToArray());
            var assembly = Assembly.Load(pe.ToArray(), pdb.ToArray());

            CompilerHelperAddMethodToken = assembly
                .GetExportedTypes()
                .First()
                .GetMethod("Add")
                .MetadataToken;

            CompilerHelperGetMethodToken = assembly
                .GetExportedTypes()
                .First()
                .GetMethod("Get")
                .MetadataToken;

            CompilerHelperTestMethodMethodToken = assembly
                .GetExportedTypes()
                .First()
                .GetMethod("TestMethod")
                .MetadataToken;
            
            return (pe, pdb);
        }

        private async Task<CorValue> LoadAssemblyIntoProcess(CorValue pe, CorValue pdb, CorEventArgs eventArgs)
        {
            var eval = eventArgs.Thread.CreateEval();
            var assemblyLoadFunction = eventArgs.Process.GetFunction(AssemblyLoadMethodToken);
            eval.CallFunction(assemblyLoadFunction, new[] {pe, pdb});
            await taskCompletionSourceWrapper.Task;
            taskCompletionSourceWrapper.Reset();
            return eval.Result;
        }

        private async Task<CorValue> LoadArrayIntoProcess(byte[] array, CorThreadEventArgs corBreakpoint)
        {
            var intWordAsString = corBreakpoint.Thread.CreateEval();
            var newIntWord = corBreakpoint.Thread.CreateEval();
            var forByteTypeEval = corBreakpoint.Thread.CreateEval();
            var createArrayEval = corBreakpoint.Thread.CreateEval();
            CorEval byteWord = corBreakpoint.Thread.CreateEval();

            // Get array length
            intWordAsString.NewString(array.Length.ToString());
            await taskCompletionSourceWrapper.Task;
            taskCompletionSourceWrapper.Reset();
            var intWordAsStringResult = intWordAsString.Result;
            var intParseFunction = corBreakpoint.Process.GetFunction(IntParseMethodToken);
            newIntWord.CallFunction(intParseFunction, new[] {intWordAsStringResult});
            await taskCompletionSourceWrapper.Task;
            taskCompletionSourceWrapper.Reset();
            var arrayLength = newIntWord.Result;
            //

            // Get Type Byte
            byteWord.NewString("System.Byte");
            await taskCompletionSourceWrapper.Task;
            taskCompletionSourceWrapper.Reset();
            var byteTypeAsString = byteWord.Result;

            // проверить, что это действительно функция Type.GetType(string);
            var createTypeFunction = corBreakpoint.Process.GetFunction(GetTypeMethodToken);
            forByteTypeEval.CallFunction(createTypeFunction, new[] {byteTypeAsString});
            await taskCompletionSourceWrapper.Task;
            taskCompletionSourceWrapper.Reset();
            var byteTypeResult = forByteTypeEval.Result;
            //

            //
            // call create array
            var createArrayInstanceFunction = corBreakpoint.Process.GetFunction(ArrayCreateInstanceMethodToken);
            createArrayEval.CallFunction(createArrayInstanceFunction, new[] {byteTypeResult, arrayLength});
            await taskCompletionSourceWrapper.Task;
            taskCompletionSourceWrapper.Reset();
            var createdArray = createArrayEval.Result;


            //                                      SyncBlock  MT of array     MT of item              
            var address = createdArray.CastToReferenceValue().Dereference().Address + PointerSize + PointerSize;
            var test = corBreakpoint.Process.WriteMemory(address, array);
            return createdArray;
        }
    }
}