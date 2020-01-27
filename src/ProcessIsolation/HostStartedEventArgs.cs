using System;

namespace ProcessIsolation
{
    public sealed class HostStartedEventArgs : EventArgs
    {
        public HostStartedEventArgs(int processId, string commandLine)
        {
            ProcessId = processId;
            CommandLine = commandLine;
        }

        public int ProcessId { get; }
        public string CommandLine { get; }
    }
}
