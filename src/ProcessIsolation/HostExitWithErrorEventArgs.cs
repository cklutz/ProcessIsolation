using System;

namespace ProcessIsolation
{
    public sealed class HostExitWithErrorEventArgs : EventArgs
    {
        public HostExitWithErrorEventArgs(int processId, int exitCode)
        {
            ProcessId = processId;
            ExitCode = exitCode;
        }

        public int ProcessId { get; }
        public int ExitCode { get; }
        public bool Handled { get; set; }
    }
}
