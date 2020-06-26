using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.CorDebug.NativeApi;

namespace Immediate
{
    public delegate void OnBreakWithModule(object sender, DynamicCorModule module, CorThreadEventArgs threadEventArgs);

    public class StartDebug
    {
        private readonly string _pathToExecutableDirectory;
        private readonly string _executableName;

        public StartDebug(string pathToExecutableDirectory, string executableName)
        {
            _pathToExecutableDirectory = pathToExecutableDirectory;
            _executableName = executableName;
        }

        public void Start(OnBreakWithModule debugEvent, EvalEventHandler evalEventHandler,
            EvalEventHandler evalExceptionHandler)
        {
            Guid classId = new Guid("9280188D-0E8E-4867-B30C-7FA83884E8DE"); //TODO: Constant with explanatory names
            Guid interfaceId = new Guid("D332DB9E-B9B3-4125-8207-A14884F53216");

            //Metadata updates have an MVID in them that is supposed to match the original assembly. The updates here
            //weren't created that way. Thankfully there is a magic internal environment variable that disables these checks.
            //The checks happen both in the debugger and the debuggee, but setting it here will propogate it to the debuggee
            //when we launch it later.
            Environment.SetEnvironmentVariable("COMPLUS_MD_DeltaCheck", "0");

            dynamic rawMetaHost;
            NativeMethods.CLRCreateInstance(ref classId, ref interfaceId, out rawMetaHost);

            ICLRMetaHost metaHost = (ICLRMetaHost) rawMetaHost;

            var currentProcess = Process.GetCurrentProcess();
            var runtime_v40 = GetLoadedRuntimeByVersion(metaHost, currentProcess.Id, "v4.0");
            var debugger = CreateDebugger(runtime_v40.m_runtimeInfo);

            //For some reason when we attach the debugger or create a process, all threads are stopped.
            //If we don't wait long enough, the assemblies and modules aren't loaded. 
            //I need to figure out how to let execution continue while the debugger is attached.

            //Same as above, if we create the process it's immediately suspended
            var corProcess = debugger.CreateProcess(_executableName, "", _pathToExecutableDirectory, 0x10);
            corProcess.OnModuleLoad += CorProcess_OnModuleLoad;
            corProcess.OnFunctionRemapOpportunity += CorProcess_OnFunctionRemapOpportunity;
            corProcess.OnFunctionRemapComplete += CorProcess_OnFunctionRemapComplete;
            corProcess.OnEvalComplete += evalEventHandler;
            corProcess.OnException += (sender, args) => { };
            corProcess.OnDebuggerError += (sender, args) =>
            {
                
            };

            corProcess.OnEvalException += evalExceptionHandler;
            corProcess.OnBreak += (sender, args) => { debugEvent(sender, ExecutionProcessModule, args); };

            corProcess.Continue(false);
            Console.ReadLine();
        }

        private static CorDebugger CreateDebugger(ICLRRuntimeInfo runtime)
        {
            var classId = new Guid("DF8395B5-A4BA-450B-A77C-A9A47762C520");
            var interfaceId = new Guid("3D6F5F61-7538-11D3-8D5B-00104B35E7EF");

            Object res = runtime.GetInterface(ref classId, ref interfaceId);
            ICorDebug debugger = (ICorDebug) res;
            //We have to initialize and set a callback in order to hook everything up
            var corDebugger = new CorDebugger(debugger);
            return corDebugger;
        }

        private static void CorProcess_OnFunctionRemapComplete(object sender, CorFunctionRemapCompleteEventArgs e)
        {
            Console.WriteLine("Hurray, the debuggee is executing my new code");
        }

        private static void CorProcess_OnFunctionRemapOpportunity(object sender, CorFunctionRemapOpportunityEventArgs e)
        {
            //A remap opportunity is where the runtime can hijack the thread IP from the old version of the code and
            //put it in the new version of the code. However the runtime has no idea how the old IL relates to the new
            //IL, so it needs the debugger to tell it which new IL offset in the updated IL is the semantically equivalent of
            //old IL offset the IP is at right now.
            Console.WriteLine("The debuggee has hit a remap opportunity at: " + e.OldFunction + ":" + e.OldILOffset);

            //I have no idea what this new IL looks like either, but lets start at the beginning of the method once again
            int newILOffset = e.OldILOffset;
            var canSetIP = e.Thread.ActiveFrame.CanSetIP(newILOffset);
            Console.WriteLine("Can set IP to: " + newILOffset + " : " + canSetIP);
            e.Thread.ActiveFrame.RemapFunction(newILOffset);
            Console.WriteLine("Continuing the debuggee in the updated IL at IL offset: " + newILOffset);
        }

        private static DynamicCorModule ExecutionProcessModule;

        private static void CorProcess_OnModuleLoad(object sender, CorModuleEventArgs e)
        {
            var module = e.Module;
            if (!module.Name.Contains("SampleProcess"))
            {
                return;
            }

            ExecutionProcessModule = e.Module;
            Console.Write("Is running: " + e.Process.IsRunning());
            var compilerFlags = module.JITCompilerFlags;
            module.JITCompilerFlags = CorDebugJITCompilerFlags.CORDEBUG_JIT_ENABLE_ENC |
                                      CorDebugJITCompilerFlags.CORDEBUG_JIT_DISABLE_OPTIMIZATION;
            ;
        }

        public static CLRRuntimeInfo GetLoadedRuntimeByVersion(ICLRMetaHost metaHost, Int32 processId, string version)
        {
            IEnumerable<CLRRuntimeInfo> runtimes = EnumerateLoadedRuntimes(metaHost, processId);

            foreach (CLRRuntimeInfo rti in runtimes)
            {
                if (rti.GetVersionString().StartsWith(version, StringComparison.OrdinalIgnoreCase))
                {
                    return rti;
                }
            }

            return null;
        }

        public static IEnumerable<CLRRuntimeInfo> EnumerateLoadedRuntimes(ICLRMetaHost metaHost, Int32 processId)
        {
            List<CLRRuntimeInfo> runtimes = new List<CLRRuntimeInfo>();
            IEnumUnknown enumRuntimes;

            using (ProcessSafeHandle hProcess = NativeMethods.OpenProcess(
                (int) (NativeMethods.ProcessAccessOptions.ProcessVMRead |
                       NativeMethods.ProcessAccessOptions.ProcessQueryInformation |
                       NativeMethods.ProcessAccessOptions.ProcessDupHandle |
                       NativeMethods.ProcessAccessOptions.Synchronize),
                false, // inherit handle
                processId))
            {
                if (hProcess.IsInvalid)
                {
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }

                enumRuntimes = metaHost.EnumerateLoadedRuntimes(hProcess);
            }

            // Since we're only getting one at a time, we can pass NULL for count.
            // S_OK also means we got the single element we asked for.
            for (object oIUnknown; enumRuntimes.Next(1, out oIUnknown, IntPtr.Zero) == 0; /* empty */)
            {
                runtimes.Add(new CLRRuntimeInfo(oIUnknown));
            }

            return runtimes;
        }
    }
}