using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using JKang.IpcServiceFramework;
using ProcessIsolation.Shared;
using ProcessIsolation.Shared.Ipc;
using ProcessIsolation.Shared.Platform;

namespace ProcessIsolation
{
    public sealed class ProcessIsolator : IDisposable
    {
        public static async Task<ProcessIsolator> CreateAsync(IsolationOptions options)
        {
            options ??= new IsolationOptions();

            var isolator = new ProcessIsolator(options);

            try
            {
                await isolator.StartAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                isolator.Dispose();
                throw;
            }

            return isolator;
        }

        // ------------------------------------------------------------------------------------------------

        private static long s_nextId;

        private readonly IsolationOptions m_options;
        private HostProcessClient m_process;
        private int m_restartAttempts;
        private readonly string m_pipeName;
        private readonly IpcServiceClient<IHostProcess> m_client;

        private ProcessIsolator(IsolationOptions options)
        {
            m_options = options ?? throw new ArgumentNullException(nameof(options));
            m_options.SanitizeAndSetReadOnly();

            Id = Interlocked.Increment(ref s_nextId);
            m_pipeName = "pihost." + Id + "." + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            m_client = new IpcServiceClientBuilder<IHostProcess>().UseNamedPipe(m_pipeName).Build();
        }

        public long Id { get; }

        public Task<int> InvokeMethodAsync(string assemblyPath, string typeAndMethodName, string[] args, CancellationToken token = default)
        {
            return m_client.InvokeAsync(s => s.InvokeMethod(assemblyPath, typeAndMethodName, args), token);
        }

        public Task<ResourceUsage> GetResourceUsageAsync(CancellationToken token = default)
        {
            return m_client.InvokeAsync(s => s.GetResourceUsage(), token);
        }

        public Task SetDataAsync(string name, object value, CancellationToken token = default)
        {
            return m_client.InvokeAsync(s => s.SetData(name, value), token);
        }

        public Task<object> GetDataAsync(string name, CancellationToken token = default)
        {
            return m_client.InvokeAsync(s => s.GetData(name), token);
        }

        public Task ExitAsync(string reason = null, CancellationToken token = default)
        {
            return m_client.InvokeAsync(s => s.Quit(reason), token);
        }

        public void Dispose()
        {
            ResetFull();
        }

        private void ResetEvents()
        {
            if (m_options.RestartAfterCrash)
            {
                m_process.EnableRaisingEvents = false;
                m_process.Exited -= OnExited;
            }
        }

        private void ResetFull()
        {
            if (m_process != null)
            {
                ResetEvents();

                m_process.OutputDataReceived -= m_options.Events.OnOutputDataReceived;
                m_process.ErrorDataReceived -= m_options.Events.OnErrorDataReceived;

                m_process.Close();
                m_process = null;
            }
        }

        public void Terminate() => Terminate(Timeout.Infinite);

        public bool Terminate(int milliseconds)
        {
            if (!m_process.HasExited)
            {
                ResetEvents();

                try
                {
                    m_process.Kill();
                }
                catch (InvalidOperationException)
                {
                    // Already exited
                }

                return m_process.WaitForFinalExit(milliseconds);
            }

            return true;
        }

        public Task<bool> WaitForExitAsync(CancellationToken token = default) => WaitForExitAsync(TimeSpan.Zero, token);

        public async Task<bool> WaitForExitAsync(TimeSpan timeout, CancellationToken token = default)
        {
            var sw = Stopwatch.StartNew();

loop:
            if (token.IsCancellationRequested)
            {
                return false;
            }

            if (m_process != null)
            {
                int pid = m_process.Id;
                if (!await m_process.WaitForExitAsync(timeout, token).ConfigureAwait(false))
                {
                    return false;
                }

                if (m_options.RestartAfterCrash && pid != m_process.Id)
                {
                    // Process has been restarted. The "OnExit()" method is called during
                    // WaitForExit(). So technically, the process has exited, but has been
                    // restarted while the user has been waiting for it. So from the user's
                    // point if view it is still running.
                    // Account for total wait time, if any, and continue waiting.

                    m_options.Events.OnHostRestart(pid, m_process.Id);

                    if (timeout != TimeSpan.Zero)
                    {
                        timeout = timeout.Add(-sw.Elapsed);

                        if (timeout <= TimeSpan.Zero)
                        {
                            return false;
                        }
                    }

                    goto loop;
                }

                int exitCode = m_process.ExitCode;
                if (exitCode != 0)
                {
                    if (!m_options.Events.OnHostExitWithError(pid, exitCode))
                    {
                        throw new IsolationException(string.Format(CultureInfo.InvariantCulture,
                            "Host process failed with code {0}",
                            ProcessUtils.GetExitCodeDescription(exitCode)));
                    }
                }
            }

            return true;
        }

        private void OnExited(object sender, EventArgs args)
        {
            int processId = m_process.Id;
            int exitCode = m_process.ExitCode;

            m_restartAttempts++;

            if (m_restartAttempts > m_options.MaxRestartAttempts)
            {
                if (!m_options.Events.OnHostRestartAttemptsExceeded(m_process.CommandLine, processId, exitCode, m_options.MaxRestartAttempts))
                {
                    throw new IsolationException(string.Format(CultureInfo.InvariantCulture,
                        "Isolation host '{0}' (process ID {1}) exited unexpectedly with exit code '{2}'. " +
                        "The previous {3} restart attempts failed and no further restarts will be attempted.",
                        m_process.HostBinary, processId, ProcessUtils.GetExitCodeDescription(exitCode),
                        m_options.MaxRestartAttempts));
                }
            }
            else
            {
                m_options.Events.OnHostRestartAttempt(m_process.CommandLine, processId, exitCode, m_restartAttempts, m_options.MaxRestartAttempts);
                ResetFull();
                StartAsync().GetAwaiter().GetResult();
            }
        }

        private async Task StartAsync(CancellationToken token = default)
        {
            if (m_process != null)
            {
                throw new InvalidOperationException($"Host process is already running (process ID {m_process.Id})");
            }

            string commandLine = null;
            try
            {
                m_process = new HostProcessClient(m_client, m_pipeName, m_options);
                commandLine = m_process.CommandLine;
                m_process.ErrorDataReceived += m_options.Events.OnErrorDataReceived;
                m_process.OutputDataReceived += m_options.Events.OnOutputDataReceived;
                m_process.EnableRaisingEvents = true;

                if (m_options.RestartAfterCrash)
                {
                    m_process.Exited += OnExited;
                }

                if (await m_process.StartAsync(m_options.StartWaitTimeout, token).ConfigureAwait(false))
                {
                    m_options.Events.OnHostStarted(m_process.Id, commandLine);
                }
                else
                {
                    m_process.Dispose();
                    m_process = null;

                    if (!m_options.Events.OnHostStartFailed(commandLine, null))
                    {
                        throw new IsolationException("Failed to start host process");
                    }
                }
            }
            catch (Exception ex)
            {
                m_process?.Dispose();
                m_process = null;

                if (!m_options.Events.OnHostStartFailed(commandLine, ex))
                {
                    throw new IsolationException("Failed to start host process", ex);
                }
            }
        }

        public override string ToString()
        {
            return Id + " / " + m_pipeName;
        }
    }
}
