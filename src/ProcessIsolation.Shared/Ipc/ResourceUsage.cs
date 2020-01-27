using System;
using System.Diagnostics;

namespace ProcessIsolation.Shared.Ipc
{
    public class ResourceUsage
    {
        public static ResourceUsage Create()
        {
            var usage = new ResourceUsage();

            var process = Process.GetCurrentProcess();

            usage.StartTime = process.StartTime;
            usage.VirtualMemorySize64 = process.VirtualMemorySize64;
            usage.WorkingSet64 = process.WorkingSet64;
            usage.TotalProcessorTime = process.TotalProcessorTime;

            var mi = GC.GetGCMemoryInfo();
            usage.GCSurvivedMemorySize = mi.HeapSizeBytes - mi.FragmentedBytes;
            usage.GCTotalAllocatedMemorySize = GC.GetTotalAllocatedBytes(precise: false);

            return usage;
        }

        public DateTime StartTime { get; set; }
        public TimeSpan TotalProcessorTime { get; set; }
        public long VirtualMemorySize64 { get; set; }
        public long WorkingSet64 { get; set; }
        public long GCSurvivedMemorySize { get; set; }
        public long GCTotalAllocatedMemorySize { get; set; }
    }
}

