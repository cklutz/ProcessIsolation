using System;
using System.Diagnostics;
using System.Threading.Tasks;
using JKang.IpcServiceFramework;
using Microsoft.Extensions.Logging;
using ProcessIsolation.Shared;
using ProcessIsolation.Shared.CommandLine;
using ProcessIsolation.Shared.Ipc;
using ProcessIsolation.Shared.Platform.Win32;
using SDProcess = System.Diagnostics.Process;
using Microsoft.Extensions.DependencyInjection;

namespace ProcessIsolation.Host
{
    internal class RootCommand : HelpCommandBase
    {
        private static JobObject s_jobObject;

        private CommandOption m_maxMemory;
        private CommandOption m_affinityMask;
        private CommandOption m_maxCpu;
        private CommandArgument m_pipeName;

        public override void Configure(CommandLineApplication command, CommandContext context)
        {
            m_maxMemory = command.Option("--max-memory <BYTES>", "Maximum virtual memory to use (e.g. \"3GB\", \"200MB\")");
            m_maxCpu = command.Option("--max-cpu <PERCENT>", "Maximum CPU usage allowed (e.g. \"80\" -> 80%)");
            m_affinityMask = command.Option("--affinity-mask <mask>", "Processor affinity mask (e.g. \"cpu0 cpu2\")");
            m_pipeName = command.Argument("[PIPENAME]", "The name of the pipe to the calling process");

            base.Configure(command, context);
        }

        protected override int Execute(CommandContext context)
        {
            return Run(
                (HostCommandContext)context,
                m_pipeName.Value,
                m_maxMemory.GetInt64(),
                m_affinityMask.GetInt32(),
                m_maxCpu.GetInt32()
                ).GetAwaiter().GetResult();
        }

        private async Task<int> Run(HostCommandContext context,
                string pipeName,
                long maxMemory,
                int affinityMask,
                int maxCpu)
        {
            var currentProcess = SDProcess.GetCurrentProcess();

            var logger = context.ServiceProvider.GetService<ILoggerFactory>().CreateLogger<HostProcessImpl>();

            logger.HostProcessStart(currentProcess.Id);

            string name = "pihost." + currentProcess.Id;

            var limits = new IsolationLimits(maxMemory, maxCpu, affinityMask);
            if (limits.IsAnyEnabled)
            {
                s_jobObject = new JobObject(name);
                s_jobObject.AddProcess(currentProcess);
                s_jobObject.Enable(limits, (l, v) => logger.ProcessLimitSet(l, v));
            }

            var host = new IpcServiceHostBuilder(context.ServiceProvider)
                .AddNamedPipeEndpoint<IHostProcess>(name, pipeName, includeFailureDetailsInResponse: true)
                .Build();

            logger.IpcServiceHostStarting(pipeName);
            context.SetStartComplete();
            await host.RunAsync(context.Token).ConfigureAwait(false);
            return 0;
        }
    }
}
