using System;

namespace ProcessIsolation
{
    public sealed class HostRestartEventArgs : EventArgs
    {
        public HostRestartEventArgs(int oldProcessId, int newProcessId)
        {
            OldProcessId = oldProcessId;
            NewProcessId = newProcessId;
        }

        public int OldProcessId { get; }
        public int NewProcessId { get; }
    }
}
