using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using JKang.IpcServiceFramework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProcessIsolation.Shared.CommandLine;
using ProcessIsolation.Shared.Ipc;

namespace ProcessIsolation.Host
{
    internal partial class Program : HelpCommandBase
    {
        private static readonly ManualResetEvent s_startComplete = new ManualResetEvent(false);
        private static readonly ManualResetEvent s_shutdownInitiated = new ManualResetEvent(false);
        private static readonly CancellationTokenSource s_tokenSource = new CancellationTokenSource();

        // Communication is typically only between client and pihost, so number of listeners
        // can be small (each will allocate a managed thread).
        private const int DefaultListeners = 2;

        public static int Main(string[] args)
        {
            if (Console.IsOutputRedirected &&
                Console.IsErrorRedirected)
            {
                Console.OutputEncoding = Encoding.UTF8;
            }

            var application = new CommandLineApplication
            {
                Name = "pihost",
                FullName = "pihost",
                Description = "ProcessIsolation generic hosting process",
                ShortVersionGetter = () => FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location).ProductVersion,
                LongVersionGetter = () => FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location).ProductVersion
            };

            var serviceProvider = ConfigureServices();
            var context = new HostCommandContext(serviceProvider, s_startComplete, s_tokenSource);
            new RootCommand().Configure(application, context);

            return application.Execute(args);
        }
        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => {
                builder.AddConsole();
                builder.SetMinimumLevel(GetLogLevel());
            });
            services.AddIpc(builder =>
            {
                builder.AddNamedPipe(options =>
                {
                    options.ThreadCount = GetListenerThreadCount();
                    // As of 20.12.2019, this option requires https://github.com/cklutz/IpcServiceFramework, until PR & new nupkg
                    //options.UseThreadNames = true;
                });
                builder.AddService<IHostProcess, HostProcessImpl>(CreateHostProcess);
            });

            return services.BuildServiceProvider();
        }

        private static LogLevel GetLogLevel()
        {
            string str = Environment.GetEnvironmentVariable("PROCISO_LOGLEVEL");
            if (str != null && Enum.TryParse<LogLevel>(str, true, out var logLevel))
            {
                return logLevel;
            }

            return LogLevel.Warning;
        }

        private static int GetListenerThreadCount()
        {
            int listeners = DefaultListeners;
            string str = Environment.GetEnvironmentVariable("PROCISO_LISTENER_THREADS");
            if (str != null && int.TryParse(str, out int temp) && temp > 0)
            {
                listeners = temp;
            }

            return listeners;
        }

        private static HostProcessImpl CreateHostProcess(IServiceProvider sp)
        {
            var factory = sp.GetService<ILoggerFactory>();
            var logger = factory.CreateLogger<HostProcessImpl>();

            return new HostProcessImpl(s_tokenSource, s_startComplete, s_shutdownInitiated, logger);
        }
    }
}
