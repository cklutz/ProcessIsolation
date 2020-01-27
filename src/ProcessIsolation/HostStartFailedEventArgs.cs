using System;

namespace ProcessIsolation
{
    public sealed class HostStartFailedEventArgs : EventArgs
    {
        public HostStartFailedEventArgs(string commandLine, Exception error)
        {
            CommandLine = commandLine;
            Error = error;
        }

        public string CommandLine { get; }
        public Exception Error { get; }
        public bool Handled { get; set; }
    }
}
