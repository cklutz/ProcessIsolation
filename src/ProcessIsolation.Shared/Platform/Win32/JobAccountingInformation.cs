using System;

namespace ProcessIsolation.Shared.Platform.Win32
{
    public class JobAccountingInformation
    {
        public TimeSpan UserProcessorTime { get; internal set; }
        public TimeSpan KernelProcessorTime { get; internal set; }
        public TimeSpan TotalProcessorTime => UserProcessorTime + KernelProcessorTime;
        public long IOReadBytes { get; internal set; }
        public long IOWriteBytes { get; internal set; }
        public long IOOtherBytes { get; internal set; }
        public long IOReadOperationsCount { get; internal set; }
        public long IOWriteOperationsCount { get; internal set; }
        public long IOOtherOperationsCount { get; internal set; }
        public long PeakJobMemory { get; internal set; }
        public long PeakProcessMemory { get; internal set; }
    }
}
