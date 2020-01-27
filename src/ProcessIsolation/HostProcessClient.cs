using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JKang.IpcServiceFramework;
using Microsoft.Extensions.Logging;
using ProcessIsolation.Shared.Ipc;
using ProcessIsolation.Shared.Platform.Win32;

namespace ProcessIsolation
{
    internal class HostProcessClient : IDisposable
    {
        private static readonly Lazy<Locations> s_locations = new Lazy<Locations>(() => new Locations());

        private Process m_process;
        private JobObject m_jobObject;
        private bool m_ioEnabled;
        private readonly IpcServiceClient<IHostProcess> m_client;

        public HostProcessClient(IpcServiceClient<IHostProcess> client, string pipeName, IsolationOptions options)
        {
            m_client = client;
            PipeName = pipeName;
            m_process = new Process();

            var arguments = new StringBuilder();
            if (!options.StartWithExeLauncher)
            {
                HostBinary = Path.GetFileName(s_locations.Value.HostAssemblyPath);
                m_process.StartInfo = new ProcessStartInfo(s_locations.Value.DotNetExecutablePath);
                arguments.Append(s_locations.Value.HostAssemblyPath);
            }
            else
            {
                HostBinary = Path.GetFileName(s_locations.Value.HostExecutablePath);
                m_process.StartInfo = new ProcessStartInfo(s_locations.Value.HostExecutablePath);
            }

            if (options.CreateJobObject)
            {
                CreateJobObject = true;
                JobName = PipeName; // For now, simply reuse pipe name
                m_jobObject = new JobObject(JobName);

                if (options.DieOnCrash)
                {
                    // Normally, we don't want the controlling process to die when a host process
                    // crashes/exits. However, the user might want to bind their faiths together
                    // for easier management.
                    m_jobObject.AddProcess(Process.GetCurrentProcess());
                }
            }

            if (options.Limits != null && options.Limits.IsAnyEnabled)
            {
                if (options.Limits.MaxCpuUsage > 0)
                {
                    arguments.Append(" --max-cpu ").Append(options.Limits.MaxCpuUsage
                        .ToString(CultureInfo.InvariantCulture));
                }

                if (options.Limits.MaxMemory > 0)
                {
                    arguments.Append(" --max-memory ").Append(options.Limits.MaxMemory
                        .ToString(CultureInfo.InvariantCulture));
                }

                if (options.Limits.AffinityMask != IntPtr.Zero)
                {
                    arguments.Append(" --affinity-mask ").Append(options.Limits.AffinityMask
                        .ToInt32().ToString(CultureInfo.InvariantCulture));
                }
            }

            Dictionary<string, string> environment = null;
            AddDebug(ref environment, options.Debug);
            AddLogLevel(ref environment, options.LogLevel);
            AddListenerThreads(ref environment, options.ListenerThreads);

            if (arguments.Length > 0)
            {
                arguments.Append(" ");
            }

            arguments.Append(pipeName);

            m_process.StartInfo.Arguments = arguments.ToString();
            if (environment != null)
            {
                foreach (var kvp in environment)
                {
                    m_process.StartInfo.Environment.Add(kvp);
                }
            }

            m_process.StartInfo.UseShellExecute = false;
            m_process.StartInfo.CreateNoWindow = true;
            m_process.StartInfo.RedirectStandardError = true;
            m_process.StartInfo.RedirectStandardOutput = true;
            m_process.StartInfo.RedirectStandardInput = true;

            CommandLine = m_process.StartInfo.FileName + " " + m_process.StartInfo.Arguments;
        }

        private static void AddDebug(ref Dictionary<string, string> environment, bool flag)
        {
            if (flag)
            {
                environment ??= new Dictionary<string, string>();
                string existingHooks = AppContext.GetData("STARTUP_HOOKS") as string;
                if (existingHooks != null)
                {
                    environment["DOTNET_STARTUP_HOOKS"] = s_locations.Value.HookAssemblyPath +
                        Path.PathSeparator + existingHooks;
                }
                else
                {
                    environment["DOTNET_STARTUP_HOOKS"] = s_locations.Value.HookAssemblyPath;
                }
                environment["PROCISO_DEBUG_ENABLED"] = flag ? "true" : "false";
            }
        }

        private static void AddLogLevel(ref Dictionary<string, string> environment, LogLevel logLevel)
        {
            environment ??= new Dictionary<string, string>();
            environment["PROCISO_LOGLEVEL"] = logLevel.ToString();
        }

        private static void AddListenerThreads(ref Dictionary<string, string> environment, int num)
        {
            if (num > 0)
            {
                environment ??= new Dictionary<string, string>();
                environment["PROCISO_LISTENER_THREADS"] = num.ToString(CultureInfo.InvariantCulture);
            }
        }

        public override string ToString() => JobName ?? PipeName;

        public string HostBinary { get; }
        public string CommandLine { get; }
        public string PipeName { get; }
        public string JobName { get; }

        public bool EnableRaisingEvents
        {
            get => m_process.EnableRaisingEvents;
            set => m_process.EnableRaisingEvents = value;
        }
        public bool CreateJobObject { get; set; }
        public ProcessStartInfo StartInfo => m_process.StartInfo;
        public int Id => m_process.Id;
        public bool HasExited => m_process.HasExited;
        public int ExitCode => m_process.ExitCode;


        public bool WaitForFinalExit(int milliseconds) => m_process.WaitForExit(milliseconds);
        public async Task<bool> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (m_process.HasExited)
            {
                return true;
            }

            if (!await m_client.InvokeAsync(c => c.WaitForShutdownInitiation(timeout), cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            return m_process.HasExited;
        }

        public event EventHandler Exited
        {
            add => m_process.Exited += value;
            remove => m_process.Exited -= value;
        }
        public event DataReceivedEventHandler ErrorDataReceived
        {
            add => m_process.ErrorDataReceived += value;
            remove => m_process.ErrorDataReceived -= value;
        }
        public event DataReceivedEventHandler OutputDataReceived
        {
            add => m_process.OutputDataReceived += value;
            remove => m_process.OutputDataReceived -= value;
        }

        public void Dispose() => CleanupProcess();
        public void Kill() => m_process.Kill();
        public void Close() => CleanupProcess();

        public Task<bool> StartAsync(CancellationToken token = default) => StartAsync(TimeSpan.Zero, token);

        public async Task<bool> StartAsync(TimeSpan startTimeout, CancellationToken token = default)
        {
            if (m_process.Start())
            {
                if (CreateJobObject)
                {
                    m_jobObject.AddProcess(m_process);
                }

                m_process.StandardInput.Close();
                m_process.BeginErrorReadLine();
                m_process.BeginOutputReadLine();
                m_ioEnabled = true;

                if (startTimeout == TimeSpan.Zero)
                {
                    await m_client.InvokeAsync(c => c.WaitForStartupComplete(Timeout.InfiniteTimeSpan), token).ConfigureAwait(false);
                }
                else
                {
                    if (!await m_client.InvokeAsync(c => c.WaitForStartupComplete(startTimeout), token).ConfigureAwait(false))
                    {
                        throw new TimeoutException($"Host {ToString()} didn't signal startup complete in {startTimeout}");
                    }
                }

                return true;
            }

            return false;
        }

        private void CleanupProcess()
        {
            if (m_process != null)
            {
                if (m_ioEnabled)
                {
                    m_process.CancelErrorRead();
                    m_process.CancelOutputRead();
                    m_ioEnabled = false;
                }

                m_process.Close();
                m_process = null;
            }

            if (m_jobObject != null)
            {
                m_jobObject.Close();
                m_jobObject = null;
            }
        }
    }
}
