using System;

namespace ProcessIsolation
{
    public sealed class HostRestartAttemptEventArgs : EventArgs
    {
        public HostRestartAttemptEventArgs(string executable,
            int processId, int exitCode, int currentRestartAttempt,
            int maxRestartAttempts)
        {
            Executable = executable;
            ProcessId = processId;
            ExitCode = exitCode;
            CurrentRestartAttempt = currentRestartAttempt;
            MaxRestartAttempts = maxRestartAttempts;
        }

        public string Executable { get; }
        public int ProcessId { get; }
        public int ExitCode { get; }
        public int CurrentRestartAttempt { get; }
        public int MaxRestartAttempts { get; }
    }
}
