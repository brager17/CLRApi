using System.Linq;
using Microsoft.Samples.Debugging.CorDebug;

namespace Immediate
{
    internal class Program
    {
        private static TaskCompletionSourceResettable
            taskCompletionSourceWrapper = new TaskCompletionSourceResettable();

        private static bool CompilerHelperLoaded = false;

        public static void Main()
        {
            new StartDebug(@"C:/Users/evgeniy/RiderProjects/Immediate/SampleProcess/bin/Debug", "SampleProcess.exe")
                .Start(async (sender, executionProcessModule, args) =>
                    {
                        var castedSender = (CorProcess) sender;
                        var eval = args.Thread.CreateEval();
                        var function = castedSender.GetFunction(100663301);
                        eval.CallFunction(function, new CorValue[] { });
                        await taskCompletionSourceWrapper.Task;
                        taskCompletionSourceWrapper.Reset();
                        if (!CompilerHelperLoaded)
                        {
                            await new CompilerHelperLoader(taskCompletionSourceWrapper).LoadCompilerHelper(args);
                            CompilerHelperLoaded = true;
                        }

                        // var eval = args.Thread.CreateEval();
                        // var function = castedSender.GetFunction(100663301);
                        // eval.CallFunction(function, new CorValue[] { });
                        // await taskCompletionSourceWrapper.Task;
                        // taskCompletionSourceWrapper.Reset();

                        //     new Interpreter(executionProcessModule.GetSymbolReader(), args.Thread.ActiveFrame);
                        // todo
                        // 1) Получить имена всех локальных переменных метода в формате (Имя index)
                        // 2) Научиться вызывать методы 
                        // 3) Получить значения всех полей в this
                        //
                        var interpreter = new Interpreter(
                            executionProcessModule.GetSymbolReader(),
                            args.Thread.ActiveFrame);
                        var i = interpreter.Interpret("var a = 1");

                        foreach (var (name, value) in i.addedToStore)
                        {
                            await CompilerHelperWithSynchronize.Add(
                                args.Thread.CreateEval(),
                                GetDebuggerAssembly(castedSender),
                                taskCompletionSourceWrapper,
                                name, (int) value
                            );
                        }

                        // interpreter.Interpret("var b = 2");
                        // var result = interpreter.Interpret("a+b").Result;
                        // // var namespaces = executionProcessModule.GetSymbolReader().GetNamespaces();
                        //
                        // // args.Thread.CreateEval().CallFunction(args.Thread.ActiveFrame.Function, new CorValue[] { });
                        //
                        // await WriteThreeItemsToArray(args);
                        castedSender.Continue(false);
                    },
                    (sender, args) => { taskCompletionSourceWrapper.SetResultTrue(); },
                    (sender, args) => { taskCompletionSourceWrapper.SetResultFalse(); }
                );
        }

        private static DynamicCorModule GetDebuggerAssembly(CorProcess process) =>
            process.Modules.First(x => x.Name == CompilerHelperLoader.DebuggerAssemblyName);
    }
}