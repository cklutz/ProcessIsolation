using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProcessIsolation.Shared.Platform.Win32
{
    public interface IJobObjectInformation
    {
        string Name { get; }
        bool CreatedNew { get; }

        /// <seealso cref="Refresh()"/>
        int CpuRateLimit { get; }
        /// <seealso cref="Refresh()"/>
        int Weight { get; }
        /// <seealso cref="Refresh()"/>
        JobMinMaxValue RateMinMaxLimit { get; }
        /// <seealso cref="Refresh()"/>
        JobMinMaxValue WorkingSetLimit { get; }
        /// <seealso cref="Refresh()"/>
        bool KillProcessesOnJobClose { get; }
        /// <seealso cref="Refresh()"/>
        bool DieOnUnhandledException { get; }
        /// <seealso cref="Refresh()"/>
        bool AllowChildProcessesBreakaway { get; }
        /// <seealso cref="Refresh()"/>
        bool AlwaysBreakawayChildProcesses { get; }
        /// <seealso cref="Refresh()"/>
        int ActiveProcessesLimit { get; }
        /// <seealso cref="Refresh()"/>
        long ProcessMemoryLimit { get; }
        /// <seealso cref="Refresh()"/>
        long JobMemoryLimit { get; }
        /// <seealso cref="Refresh()"/>
        TimeSpan ProcessUserTimeLimit { get; }
        /// <seealso cref="Refresh()"/>
        TimeSpan JobUserTimeLimit { get; }
        /// <seealso cref="Refresh()"/>
        ProcessPriorityClass PriorityClass { get; }
        /// <seealso cref="Refresh()"/>
        int SchedulingClass { get; }
        /// <seealso cref="Refresh()"/>
        IntPtr ProcessorAffinity { get; }
        bool IsProcessInJob(Process process);
        /// <summary>
        /// Synchronizes the internal state of this instance with the underlying operating system object.
        /// </summary>
        void Refresh();
        JobAccountingInformation GetAccountingInformation();
        /// <summary>
        /// Return a list of process IDs of processes that are part of the current job.
        /// </summary>
        /// <returns></returns>
        List<int> GetProcessIds();
    }
}
