using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

public class C
{
    public static void Main()
    {
        F();
    }

    public static void F1()
    {
        Console.WriteLine(123);
    }

//     public const string Code = @"using System.Collections.Generic;
//
// namespace Immediate
// {
//     public static class CompilerHelper
//     {
//         static Dictionary<string, object> PseudoVariables = new Dictionary<string, object>();
//
//         public static object Get(string name) => PseudoVariables[name];
//
//         public static void Add(string name, object value) =>
//             PseudoVariables.Add(name, value);
//     }
// }";

    // public static void TestAssemblyLoad()
    // {
    //     var assemblies = AppDomain.CurrentDomain.GetAssemblies()
    //         .SelectMany(x => x.Modules).Select(x => x.FullyQualifiedName)
    //         .ToArray();
    //
    //     var metadata = assemblies
    //         .Select(item => MetadataReference.CreateFromFile(item)).Cast<MetadataReference>()
    //         .ToList();
    //
    //
    //     CSharpCompilation t = CSharpCompilation.Create("debuggerHelpersAssembly")
    //         .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
    //         .WithReferences(metadata)
    //         .AddSyntaxTrees(SyntaxFactory.ParseSyntaxTree(Code));
    //
    //     var pe = new MemoryStream();
    //     var pdb = new MemoryStream();
    //     var result = t.Emit(pe, pdb);
    //     if (result.Success == false) throw new Exception();
    //     Assembly.Load(pe.ToArray(), pdb.ToArray());
    // }

    private static int GetTypeMetadataToken()
    {
        var methodInfo = typeof(Type).GetMethods().Where(x => x.Name == "GetType").ToArray()[3];
        return methodInfo.MetadataToken;
    }

    public static string Method() => "quer";

    public static int GetMetadataTokenAssemblyLoad()
    {
        return 1;
        // var method = typeof(Assembly).GetMethods(BindingFlags.Static | BindingFlags.Public)
        // .First(x => x.Name == "Load" && x.GetParameters().Length == 2
                                         // && x.GetParameters().First().ParameterType == typeof(byte[])
                                         // && x.GetParameters().Last().ParameterType == typeof(byte[]));

        // return method.MetadataToken;
    }

    private static int GetMetadataTokenIntParse()
    {
        var parseMethods = typeof(int).GetMethods().First(x => x.Name == "Parse" && x.GetParameters().Length == 1);
        return parseMethods.MetadataToken;
    }

    public static int GetMetadataArrayCreateInstance()
    {
        var arrayCreateInstance = typeof(Array).GetMethods().First(x =>
            x.Name == "CreateInstance" && x.GetParameters().Length == 2);
        return arrayCreateInstance.MetadataToken;
    }


    public static void F()
    {
        var t = 1;
        var tt = 2;
        var z = 14;
        GetMetadataTokenAssemblyLoad();
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        Debugger.Break();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Console.WriteLine(assembly.FullName);
            if (assembly.FullName.Contains("debug"))
            {
                Console.WriteLine(1);
                var exportedTypes = assembly.GetExportedTypes();
                Console.WriteLine("Types: ");
                foreach (var exportedType in exportedTypes)
                {
                    if (exportedType.Name == "CompilerHelper")
                    {
                        foreach (var methodInfo in exportedType.GetMethods())
                        {
                            Console.WriteLine($"Metadata token {methodInfo.Name}: " + methodInfo.MetadataToken);
                        }
                    }

                    Console.WriteLine(exportedType.Name);
                }

                Console.WriteLine();

                var value = (Dictionary<string, object>) exportedTypes
                    .First(x => x.Name == "CompilerHelper")
                    .GetField("PseudoVariables")
                    .GetValue(null);
                Console.WriteLine(value.ToString());
                Console.WriteLine(value.Count);
                foreach (var keyValue in value)
                {
                    Console.WriteLine($"Key {keyValue.Key}; Value {keyValue.Value}");
                }
            }
        }


        Console.ReadKey();
    }
}