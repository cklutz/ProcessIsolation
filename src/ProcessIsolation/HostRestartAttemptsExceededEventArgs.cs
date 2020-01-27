using System;

namespace ProcessIsolation
{
    public sealed class HostRestartAttemptsExceededEventArgs : EventArgs
    {
        public HostRestartAttemptsExceededEventArgs(string executable,
            int processId, int exitCode, int maxRestartAttempts)
        {
            Executable = executable;
            ProcessId = processId;
            ExitCode = exitCode;
            MaxRestartAttempts = maxRestartAttempts;
        }

        public string Executable { get; }
        public int ProcessId { get; }
        public int ExitCode { get; }
        public int MaxRestartAttempts { get; }
        public bool Handled { get; set; }
    }
}
