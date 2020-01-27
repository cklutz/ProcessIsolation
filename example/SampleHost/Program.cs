using Microsoft.Extensions.Logging;
using ProcessIsolation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace SampleHost
{
    internal static class Program
    {
        private const string Usage = @"
Usage: {0} [options] [-- arguments to method]

Run a method from an assembly in an external host process.
If no options are specified, built-in defaults are assumed.

Options:
 --assembly | -a PATH         Call method from assembly (e.g ""foo.dll"")
 --type-and-method | -m TAM   Call method from type (e.g. ""Namespace.Class.MethodName"")
 --restart                    Restart host when it crashes
   --max-restart-attempts NUM Maximum restart attempts
 --die-on-crash               Die when host crashes
 --debug                      Host process will be suspended until a debugger is attached
 --log-level LOGLEVEL         Set log level for host process (debug, info, warning, error)
 --max-cpu PERCENTAGE         Maximum CPU usage for host process (e.g. ""80"" -> 80%).
 --max-memory BYTES           Maximum virtual memory for host process (e.g. ""3GB"", ""200MB"")
 --affinity-mask MASK         Processor affinity mask for host process (e.g. ""cpu0 cpu2"")
";

        static async Task<int> Main(string[] args)
        {
            try
            {
                string lib = null;
                string tam = null;
                var options = new IsolationOptions();
                bool argDelimiterSeen = false;
                var margs = new List<string>();

                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--help" || args[i] == "-h" || args[i] == "-?")
                    {
                        Console.Error.WriteLine(Usage, typeof(Program).Assembly.GetName().Name);
                        return 1;
                    }

                    if (args[i] == "--")
                    {
                        argDelimiterSeen = true;
                    }
                    else
                    {
                        if (argDelimiterSeen)
                        {
                            margs.Add(args[i]);
                        }
                        else
                        {
                            if (!TryGetOption(ref i, args, "--assembly", s => lib = Path.GetFullPath(s)) &&
                                !TryGetOption(ref i, args, "-a", s => lib = Path.GetFullPath(s)) &&
                                !TryGetOption(ref i, args, "--type-and-method", s => tam = s) &&
                                !TryGetOption(ref i, args, "-m", s => tam = s) &&
                                !TryGetSwitch(ref i, args, "--use-exe-launcher", f => options.StartWithExeLauncher = f) &&
                                !TryGetSwitch(ref i, args, "--restart", f => options.RestartAfterCrash = f) &&
                                !TryGetSwitch(ref i, args, "--debug", f => options.Debug = f) &&
                                !TryGetSwitch(ref i, args, "--die-on-crash", f => options.DieOnCrash = f) &&
                                !TryGetOption(ref i, args, "--log-level", s => options.LogLevel = (LogLevel)Enum.Parse(typeof(LogLevel), s, true)) &&
                                !TryGetOption(ref i, args, "--max-restart-attempts", s => options.MaxRestartAttempts = int.Parse(s)) &&
                                !TryGetOption(ref i, args, "--max-cpu", s => options.Limits = options.Limits.WithMaxCpu(s)) &&
                                !TryGetOption(ref i, args, "--max-memory", s => options.Limits = options.Limits.WithMaxMemory(s)) &&
                                !TryGetOption(ref i, args, "--affinity-mask", s => options.Limits = options.Limits.WithAffinityMask(s)))
                            {
                                Console.Error.WriteLine("Warning: Unknown option '{0}'", args[i]);
                            }
                        }
                    }
                }

                if (lib == null)
                {
                    lib = Path.Combine(
                        Path.GetDirectoryName(typeof(Program).Assembly.Location),
                        @"..\..\..\..\SampleLib\bin\Debug\netcoreapp3.1\SampleLib.dll");

                    if (!File.Exists(lib))
                    {
                        lib = Path.Combine(
                            Path.GetDirectoryName(typeof(Program).Assembly.Location),
                            @"..\..\..\..\..\SampleLib\bin\Debug\netcoreapp3.1\publish\SampleLib.dll");
                    }
                }

                if (tam == null)
                {
                    tam = "SampleLib.SampleClass.Method";
                }

                options.Events.HostStarted += (s, d) => Console.WriteLine("STARTED " + d.ProcessId + ", " + d.CommandLine);
                options.Events.HostStartFailed += (s, d) => Console.WriteLine("START FAILED: " + d.CommandLine);
                options.Events.HostErrorDataReceived += (s, d) => Console.Error.WriteLine("PIHOST> " + d.Data);
                options.Events.HostOutputDataReceived += (s, d) => Console.WriteLine("PIHOST> " + d.Data);
                options.Events.HostRestart += (s, d) => Console.WriteLine("RESTARTING DETECTED");
                options.Events.HostRestartAttempt += (s, d) => Console.WriteLine("RESTART ATTEMPT");
                options.Events.HostRestartAttemptsExceeded += (s, d) => Console.WriteLine("RESTART ATTEMPTS EXCEEDED");
                options.Events.HostExitWithError += (s, d) => Console.WriteLine("EXIT: " + d.ProcessId + " code " + d.ExitCode);

                using (var isolator = await ProcessIsolator.CreateAsync(options).ConfigureAwait(false))
                {
                    //var usage = await isolator.GetResourceUsageAsync().ConfigureAwait(false);
                    //Console.WriteLine("XXX: " + usage.StartTime);
                    //Console.WriteLine("XXX: " + usage.VirtualMemorySize64);

                    try
                    {
                        int res = await isolator.InvokeMethodAsync(lib, tam, margs.ToArray()).ConfigureAwait(false);
                        Console.WriteLine("RESULT: " + res);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("EXCEPTION: " + ex);
                    }

                    //isolator.Terminate();
                    await isolator.WaitForExitAsync().ConfigureAwait(false);
                }

                Console.WriteLine("COMPLETE");
                return 0;
            }
            catch (CommandLineException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return ex.HResult;
            }
        }

        static bool TryGetOption(ref int i, string[] args, string option, Action<string> set)
        {
            if (args[i] == option)
            {
                if (i + 1 >= args.Length)
                {
                    throw new CommandLineException($"Not enough arguments: missing value for {option}");
                }

                set.Invoke(args[++i]);
                return true;
            }

            return false;
        }

        static bool TryGetSwitch(ref int i, string[] args, string option, Action<bool> set)
        {
            if (args[i] == option)
            {
                set.Invoke(true);
                return true;
            }

            return false;
        }

        [Serializable]
        public class CommandLineException : Exception
        {
            public CommandLineException() { }
            public CommandLineException(string message) : base(message) { }
            public CommandLineException(string message, Exception inner) : base(message, inner) { }
            protected CommandLineException(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
            }
        }
    }
}
