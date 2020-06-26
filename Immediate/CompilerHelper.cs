using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.CorDebug.NativeApi;

namespace Immediate
{
    public static class CompilerHelper
    {
        public static Dictionary<string, object> PseudoVariables = new Dictionary<string, object>();

        public static object Get(string name)
        {
            Console.WriteLine($"Get method: Name {name}");
            return PseudoVariables[name];
        }

        public static void TestMethod()
        {
        }

        public static void Add(string name, object value)
        {
            Console.WriteLine($"Add method: Name {name}, value {value}");
            PseudoVariables.Add(name, value);
        }
    }

    public static class CompilerHelperWithSynchronize
    {
        public static async Task Add(CorEval corEval,
            DynamicCorModule dynamicCorModule,
            TaskCompletionSourceResettable taskCompletionSourceResettable, string name, int value)
        {
            //<-------------------------------------For testing code--------------------------------------------->
            var function = dynamicCorModule.Process.GetFunction(100663301);
            corEval.CallFunction(function, new CorValue[] { });
            await taskCompletionSourceResettable.Task;
            taskCompletionSourceResettable.Reset();
            var test = dynamicCorModule.Process.GetFunction(CompilerHelperLoader.CompilerHelperTestMethodMethodToken);
            corEval.CallFunction(test, new CorValue[] { });
            await taskCompletionSourceResettable.Task;
            taskCompletionSourceResettable.Reset();
            //<-------------------------------------For testing code--------------------------------------------->
            // implemented for int only
            corEval.NewString(value.ToString());
            await taskCompletionSourceResettable.Task;
            taskCompletionSourceResettable.Reset();
            corEval.CallFunction(
                dynamicCorModule.Process.GetFunction(CompilerHelperLoader.IntParseMethodToken),
                new[] {corEval.Result});

            await taskCompletionSourceResettable.Task;
            taskCompletionSourceResettable.Reset();

            var corValue = corEval.Result;
            corEval.NewString(name);
            await taskCompletionSourceResettable.Task;
            taskCompletionSourceResettable.Reset();
            var paramName = corEval.Result;
            //

            var add = dynamicCorModule.Process.GetFunction(CompilerHelperLoader.CompilerHelperAddMethodToken);

            corEval.CallFunction(add, new[] {paramName, corValue});
            await taskCompletionSourceResettable.Task;
            taskCompletionSourceResettable.Reset();

            // corEval.NewString("a");
            // await taskCompletionSourceResettable.Task;
            // taskCompletionSourceResettable.Reset();
            //
            // var stringValue = corEval.Result;
            // var getMethodResult =
            //     dynamicCorModule.Process.GetFunction(CompilerHelperLoader.CompilerHelperGetMethodToken);
            // corEval.CallFunction(getMethodResult, new[] {stringValue});
            // await taskCompletionSourceResettable.Task;
            // taskCompletionSourceResettable.Reset();
            // var result = corEval.Result;
            // // var get =
            // var dereferencedResult = result.CastToReferenceValue().Dereference();
            // var buffer = new byte[dereferencedResult.Size];
            // dynamicCorModule.Process.ReadMemory(dereferencedResult.Address, buffer);
        }
    }
}