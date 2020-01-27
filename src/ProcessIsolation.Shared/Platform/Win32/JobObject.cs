using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace ProcessIsolation.Shared.Platform.Win32
{
#pragma warning disable CA1060 // Move pinvokes to native methods class
#pragma warning disable CA2216 // Disposable types should declare finalizer
    public sealed class JobObject : SafeHandleZeroOrMinusOneIsInvalid, IJobObjectInformation
    {
        private uint m_activeProcessesLimit;
        private bool m_jobUserTimeLimitChanged;
        private long m_jobUserTimeLimit;
        private long m_processUserTimeLimit;
        private uint m_priorityClass;
        private uint m_schedulingClass;
        private JobMinMaxValue m_workingSetLimit;
        private int m_cpuRate;
        private JobMinMaxValue m_minMaxRate;
        private int m_weight;

        private bool m_enableRaisingEvents;
        private IntPtr m_completionPort;
        private Thread m_eventThread;
        private volatile bool m_eventThreadExit;

        public JobObject()
            : this(null, null)
        {
        }

        public JobObject(string name)
            : this(name, null)
        {
        }

        private JobObject(string name, byte b)
            : base(true)
        {
            IntPtr result;
            if (name != null)
            {
                result = OpenJobObject(JOB_OBJECT_ALL_ACCESS, false, name);
                if (result == IntPtr.Zero)
                {
                    throw GetException(Marshal.GetLastWin32Error(), $"Failed to open job object named {name}");
                }

                Name = name;
            }
            else
            {
                result = IntPtr.Zero;
                IsReadOnly = true;
                Name = ""; // Have no name available and the Win-API provides no way to get it.
            }

            SetHandle(result);
            CreatedNew = false;
            Refresh();
        }

        public bool IsReadOnly { get; }

        public override bool IsInvalid => (handle == IntPtr.Zero && !IsReadOnly) || handle == new IntPtr(-1);

        public JobObject(string name, Action<JobObject> action)
            : base(true)
        {
            if (string.IsNullOrEmpty(name))
            {
                name = null;
            }

            Name = name;

            SetHandle(AcquireJobObject(name, out var created));
            CreatedNew = created;

            if (action != null)
            {
                action(this);
            }
            else
            {
                if (created)
                {
                    Update();
                }
                else
                {
                    Refresh();
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                StopRaisingEvents();
            }
        }

        /// <summary>
        /// Returns information about the Job Object that the current process is
        /// assigned to, if any. Otherwise returns <c>null</c>.
        /// </summary>
        /// <returns>
        /// Information about the Job Object that the current process is
        /// assigned to, if any. Otherwise returns <c>null</c>.
        /// </returns>
        public static IJobObjectInformation GetCurrentJobInformation()
        {
            if (!IsProcessInAnyJob(Process.GetCurrentProcess()))
            {
                return null;
            }

            return new JobObject(null, (byte)0);
        }

        public static JobObject OpenExisting(string name)
        {
            return new JobObject(name, (byte)0);
        }

        public string Name { get; }

        public bool CreatedNew { get; }
        protected override bool ReleaseHandle()
        {
            return CloseHandle(handle);
        }

        public bool AddProcess(IntPtr processHandle)
        {
            CheckReadOnly();

            return AssignProcessToJobObject(handle, processHandle);
        }

        public bool AddProcess(int processId)
        {
            return AddProcess(Process.GetProcessById(processId).Handle);
        }

        public bool AddProcess(Process process)
        {
            if (process == null)
                throw new ArgumentNullException(nameof(process));

            return AddProcess(process.Handle);
        }

        public void Terminate(int exitCode)
        {
            CheckReadOnly();

            if (!TerminateJobObject(handle, (uint)exitCode))
            {
                throw GetException(Marshal.GetLastWin32Error(), "TerminateJobObject failed");
            }
        }

        public bool IsProcessInJob(Process process)
        {
            if (process == null)
            {
                throw new ArgumentNullException(nameof(process));
            }

            if (!IsProcessInJob(process.Handle, handle, out var result))
            {
                throw GetException(Marshal.GetLastWin32Error(), "IsProcessInJob failed");
            }

            return result;
        }

        public static bool IsProcessInAnyJob(Process process)
        {
            if (process == null)
            {
                throw new ArgumentNullException(nameof(process));
            }

            if (!IsProcessInJob(process.Handle, IntPtr.Zero, out var result))
            {
                throw GetException(Marshal.GetLastWin32Error(), "IsProcessInJob failed");
            }

            return result;
        }

        public bool EnableRaisingEvents
        {
            get => m_enableRaisingEvents;
            set
            {
                if (value)
                {
                    EnsureRaisingEvents();
                }
                else
                {
                    StopRaisingEvents();
                }
                m_enableRaisingEvents = value;
            }
        }

        public event EventHandler<JobObjectEventArgs> EventRaised;

        /// <summary>
        /// Synchronizes the internal state of this instance with the underlying operating system object.
        /// </summary>
        public void Refresh()
        {
            QueryExtendedLimitInformation(null, true);
            QueryRateInformation(true);
        }

        /// <summary>
        /// Updates the state of the underlying operating system object with the state of this instance.
        /// </summary>
        public void Update()
        {
            CheckReadOnly();

            UpdateExtendedLimit();
            UpdateRateLimit();
        }

        /// <summary>
        /// Synchronize state of this instance with the operating system state,
        /// execute an action and update the operating system state with this instance.
        /// </summary>
        /// <param name="action"></param>
        public void Update(Action<JobObject> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            Refresh();
            action(this);
            Update();
        }

        // -----------------------------------------------------------------------------------------------

        /// <seealso cref="Update()"/>
        /// <seealso cref="Refresh()"/>
        public bool EnableRateNotification { get; set; }

        /// <seealso cref="Update()"/>
        /// <seealso cref="Refresh()"/>
        public int CpuRateLimit
        {
            get => m_cpuRate;
            set
            {
                m_minMaxRate = JobMinMaxValue.Empty;
                m_weight = 0;
                m_cpuRate = value;
            }
        }

        /// <seealso cref="Update()"/>
        /// <seealso cref="Refresh()"/>
        public int Weight
        {
            get => m_weight;
            set
            {
                m_minMaxRate = JobMinMaxValue.Empty;
                m_cpuRate = 0;
                m_weight = value;
            }
        }

        /// <seealso cref="Update()"/>
        /// <seealso cref="Refresh()"/>
        public JobMinMaxValue RateMinMaxLimit
        {
            get => m_minMaxRate;
            set
            {
                m_cpuRate = 0;
                m_weight = 0;
                m_minMaxRate = value;
            }
        }

        public JobMinMaxValue WorkingSetLimit
        {
            get => m_workingSetLimit;
            set => m_workingSetLimit = value;
        }

        /// <summary>
        /// Gets or sets processHandle value indicating whether to kill the processes when the Job Object is closed.
        /// </summary>
        /// <value>
        ///  <c>true</c> if [kill processes on job close]; otherwise, <c>false</c>.
        /// </value>
        /// <seealso cref="Update()"/>
        /// <seealso cref="Refresh()"/>
        public bool KillProcessesOnJobClose { get; set; } = true;

        /// <summary>
        /// Gets or sets processHandle value indicating whether processHandle process to die on an unhandled exception.
        /// </summary>
        /// <value>
        ///  <c>true</c> if [die on unhandled exception]; otherwise, <c>false</c>.
        /// </value>
        /// <seealso cref="Update()"/>
        /// <seealso cref="Refresh()"/>
        public bool DieOnUnhandledException { get; set; }

        /// <summary>
        /// Gets or sets processHandle value indicating whether processes are allowed to create processes outside the Job Object.
        /// </summary>
        /// <value>
        ///  <c>true</c> if [allow child processes breakaway]; otherwise, <c>false</c>.
        /// </value>
        /// <seealso cref="Update()"/>
        /// <seealso cref="Refresh()"/>
        public bool AllowChildProcessesBreakaway { get; set; }

        /// <summary>
        /// Gets or sets processHandle value indicating whether child processes are not added to the Job Object.
        /// </summary>
        /// <value>
        ///  <c>true</c> if [always breakaway child processes]; otherwise, <c>false</c>.
        /// </value>
        /// <seealso cref="Update()"/>
        /// <seealso cref="Refresh()"/>
        public bool AlwaysBreakawayChildProcesses { get; set; }

        /// <summary>
        /// Gets or sets the active processes in the Job Object. Set to 0 (zero) to disable the limit.
        /// </summary>
        /// <value>
        /// The active processes.
        /// </value>
        public int ActiveProcessesLimit
        {
            get => (int)m_activeProcessesLimit;
            set => m_activeProcessesLimit = (uint)value;
        }

        /// <summary>
        /// Gets or sets the memory in bytes limit enforced per process. Set to 0 (zero) to disable the limit.
        /// </summary>
        /// <value>
        /// The process memory limit.
        /// </value>
        /// <seealso cref="Update()"/>
        /// <seealso cref="Refresh()"/>
        public long ProcessMemoryLimit { get; set; }

        /// <summary>
        /// Gets or sets the memory limit in bytes of the entire Job Object. Set to 0 (zero) to disable the limit.
        /// </summary>
        /// <value>
        /// The job memory limit.
        /// </value>
        /// <seealso cref="Update()"/>
        /// <seealso cref="Refresh()"/>
        public long JobMemoryLimit { get; set; }

        /// <summary>
        /// Gets or sets the process user time limit. It is enforced per process. Set to 0 (zero) to disable the limit.
        /// </summary>
        /// <value>
        /// The process user time limit.
        /// </value>
        /// <seealso cref="Update()"/>
        /// <seealso cref="Refresh()"/>
        public TimeSpan ProcessUserTimeLimit
        {
            get => new TimeSpan(m_processUserTimeLimit);
            set => m_processUserTimeLimit = value.Ticks;
        }

        /// <summary>
        /// Gets or sets the Job Object user time limit. Every process user time is accounted. Set to 0 (zero) to disable the limit.
        /// </summary>
        /// <value>
        /// The job user time limit.
        /// </value>
        /// <seealso cref="Update()"/>
        /// <seealso cref="Refresh()"/>
        public TimeSpan JobUserTimeLimit
        {
            get => new TimeSpan(m_jobUserTimeLimit);
            set
            {
                m_jobUserTimeLimit = value.Ticks;
                m_jobUserTimeLimitChanged = true;
            }
        }

        /// <summary>
        /// Gets or sets the priority class of the Job Object.
        /// </summary>
        /// <value>
        /// The priority class.
        /// </value>
        /// <seealso cref="Update()"/>
        /// <seealso cref="Refresh()"/>
        public ProcessPriorityClass PriorityClass
        {
            get => (ProcessPriorityClass)m_priorityClass;
            set => m_priorityClass = (uint)value;
        }

        /// <summary>
        /// Gets or sets the scheduling class of the JobObject.
        /// </summary>
        /// <value>
        /// The scheduling class.
        /// </value>
        /// <seealso cref="Update()"/>
        /// <seealso cref="Refresh()"/>
        public int SchedulingClass
        {
            get => (int)m_schedulingClass;
            set => m_schedulingClass = (uint)value;
        }

        /// <summary>
        /// Gets or sets the processor affinity, enforced for every process.
        /// </summary>
        /// <value>
        /// The affinity.
        /// </value>
        /// <seealso cref="Update()"/>
        /// <seealso cref="Refresh()"/>
        public IntPtr ProcessorAffinity { get; set; } = IntPtr.Zero;

        public JobAccountingInformation GetAccountingInformation()
        {
            var result = new JobAccountingInformation();
            QueryBasicAndIoAccounting(result);
            QueryExtendedLimitInformation(result);
            return result;
        }

        /// <summary>
        /// Return a list of process IDs of processes that are part of the current job.
        /// </summary>
        /// <returns></returns>
        public List<int> GetProcessIds() => QueryProcessIds();

        // -----------------------------------------------------------------------------------------------

        private void CheckReadOnly()
        {
            if (IsReadOnly)
            {
                throw new InvalidOperationException("Cannot change job");
            }
        }

        private void UpdateRateLimit()
        {
            CheckReadOnly();

            var rate = new JOBOBJECT_CPU_RATE_CONTROL_INFORMATION { ControlFlags = 0 };

            if (m_weight > 0)
            {
                rate.Weight = m_weight;
                rate.ControlFlags = JOB_OBJECT_CPU_RATE_CONTROL_ENABLE | JOB_OBJECT_CPU_RATE_CONTROL_WEIGHT_BASED;
            }

            if (m_cpuRate > 0)
            {
                rate.CpuRate = m_cpuRate * 100;
                rate.ControlFlags = JOB_OBJECT_CPU_RATE_CONTROL_ENABLE | JOB_OBJECT_CPU_RATE_CONTROL_HARD_CAP;
            }

            if (m_minMaxRate != JobMinMaxValue.Empty)
            {
                rate.MinRate = (short)(m_minMaxRate.Minimum * 100);
                rate.MaxRate = (short)(m_minMaxRate.Maximum * 100);
                rate.ControlFlags = JOB_OBJECT_CPU_RATE_CONTROL_ENABLE | JOB_OBJECT_CPU_RATE_CONTROL_MIN_MAX_RATE;
            }

            int size = Marshal.SizeOf(typeof(JOBOBJECT_CPU_RATE_CONTROL_INFORMATION));

            if (rate.ControlFlags == 0)
            {
                IntPtr existingRatePtr = IntPtr.Zero;
                try
                {
                    existingRatePtr = Marshal.AllocHGlobal(size);
                    bool success = QueryInformationJobObject(handle, JobObjectInfoType.CpuRateControlInformation, existingRatePtr, (uint)size, IntPtr.Zero);
                    if (!success)
                    {
                        throw GetException(Marshal.GetLastWin32Error(), "QueryInformationJobObject failed");
                    }

                    var existingRate = Marshal.PtrToStructure<JOBOBJECT_CPU_RATE_CONTROL_INFORMATION>(existingRatePtr);
                    if (existingRate.ControlFlags == 0)
                    {
                        // SetInformationJobObject() will fail ("invalid parameter") if we attempt to "reset" cpu rate information,
                        // when none is currently set.
                        return;
                    }
                }
                finally
                {
                    if (existingRatePtr != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(existingRatePtr);
                    }
                }
            }

            IntPtr ratePtr = IntPtr.Zero;

            if (EnableRateNotification)
            {
                rate.ControlFlags |= JOB_OBJECT_CPU_RATE_CONTROL_NOTIFY;
            }

            try
            {
                ratePtr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(rate, ratePtr, false);

                bool success = SetInformationJobObject(handle, JobObjectInfoType.CpuRateControlInformation, ratePtr, (uint)size);
                if (!success)
                {
                    throw GetException(Marshal.GetLastWin32Error(), "SetInformationJobObject failed");
                }
            }
            finally
            {
                if (ratePtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(ratePtr);
                }
            }
        }

        private void UpdateExtendedLimit()
        {
            CheckReadOnly();

            var extendedLimit = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            var basicLimit = new JOBOBJECT_BASIC_LIMIT_INFORMATION { LimitFlags = 0 };

            if (KillProcessesOnJobClose)
            {
                basicLimit.LimitFlags |= JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
            }

            if (DieOnUnhandledException)
            {
                basicLimit.LimitFlags |= JOB_OBJECT_LIMIT_DIE_ON_UNHANDLED_EXCEPTION;
            }

            if (AllowChildProcessesBreakaway)
            {
                basicLimit.LimitFlags |= JOB_OBJECT_LIMIT_BREAKAWAY_OK;
            }

            if (AlwaysBreakawayChildProcesses)
            {
                basicLimit.LimitFlags |= JOB_OBJECT_LIMIT_SILENT_BREAKAWAY_OK;
            }

            if (m_activeProcessesLimit != 0)
            {
                basicLimit.LimitFlags |= JOB_OBJECT_LIMIT_ACTIVE_PROCESS;
                basicLimit.ActiveProcessLimit = m_activeProcessesLimit;
            }

            if (ProcessMemoryLimit != 0)
            {
                basicLimit.LimitFlags |= JOB_OBJECT_LIMIT_PROCESS_MEMORY;
                extendedLimit.ProcessMemoryLimit = (IntPtr)ProcessMemoryLimit;
            }

            if (JobMemoryLimit != 0)
            {
                basicLimit.LimitFlags |= JOB_OBJECT_LIMIT_JOB_MEMORY;
                extendedLimit.JobMemoryLimit = (IntPtr)JobMemoryLimit;
            }

            if (m_processUserTimeLimit != 0)
            {
                basicLimit.LimitFlags |= JOB_OBJECT_LIMIT_PROCESS_TIME;
                basicLimit.PerProcessUserTimeLimit = m_processUserTimeLimit;
            }

            if (m_jobUserTimeLimit != 0)
            {
                if (m_jobUserTimeLimitChanged)
                {
                    basicLimit.LimitFlags |= JOB_OBJECT_LIMIT_JOB_TIME;
                    basicLimit.PerJobUserTimeLimit = m_jobUserTimeLimit;
                    m_jobUserTimeLimitChanged = false;
                }
                else
                {
                    basicLimit.LimitFlags |= JOB_OBJECT_LIMIT_PRESERVE_JOB_TIME;
                }
            }

            if (m_priorityClass != 0)
            {
                basicLimit.LimitFlags |= JOB_OBJECT_LIMIT_PRIORITY_CLASS;
                basicLimit.PriorityClass = m_priorityClass;
            }

            if (m_schedulingClass != 0)
            {
                basicLimit.LimitFlags |= JOB_OBJECT_LIMIT_SCHEDULING_CLASS;
                basicLimit.SchedulingClass = m_schedulingClass;
            }

            if (ProcessorAffinity != IntPtr.Zero)
            {
                basicLimit.LimitFlags |= JOB_OBJECT_LIMIT_AFFINITY;
                basicLimit.Affinity = ProcessorAffinity;
            }

            if (m_workingSetLimit != JobMinMaxValue.Empty)
            {
                basicLimit.LimitFlags |= JOB_OBJECT_LIMIT_WORKINGSET;
                basicLimit.MinimumWorkingSetSize = (UIntPtr)m_workingSetLimit.Minimum;
                basicLimit.MaximumWorkingSetSize = (UIntPtr)m_workingSetLimit.Maximum;
            }

            extendedLimit.BasicLimitInformation = basicLimit;

            int extendedLimitLength = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            IntPtr extendedLimitPtr = IntPtr.Zero;

            try
            {
                extendedLimitPtr = Marshal.AllocHGlobal(extendedLimitLength);
                Marshal.StructureToPtr(extendedLimit, extendedLimitPtr, false);

                bool success = SetInformationJobObject(handle, JobObjectInfoType.ExtendedLimitInformation, extendedLimitPtr, (uint)extendedLimitLength);
                if (!success)
                {
                    throw GetException(Marshal.GetLastWin32Error(), "SetInformationJobObject failed");
                }
            }
            finally
            {
                if (extendedLimitPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(extendedLimitPtr);
                }
            }
        }


        private void QueryRateInformation(bool updateState)
        {
            if (!updateState)
                return;

            IntPtr ratePtr = IntPtr.Zero;

            try
            {
                int size = Marshal.SizeOf(typeof(JOBOBJECT_CPU_RATE_CONTROL_INFORMATION));
                ratePtr = Marshal.AllocHGlobal(size);
                bool success = QueryInformationJobObject(handle, JobObjectInfoType.CpuRateControlInformation, ratePtr, (uint)size, IntPtr.Zero);
                if (!success)
                {
                    throw GetException(Marshal.GetLastWin32Error(), "QueryInformationJobObject failed");
                }

                var rate = Marshal.PtrToStructure<JOBOBJECT_CPU_RATE_CONTROL_INFORMATION>(ratePtr);

                m_cpuRate = 0;
                m_minMaxRate = JobMinMaxValue.Empty;
                m_weight = 0;

                if ((rate.ControlFlags & JOB_OBJECT_CPU_RATE_CONTROL_ENABLE) != 0)
                {
                    if ((rate.ControlFlags & JOB_OBJECT_CPU_RATE_CONTROL_HARD_CAP) != 0)
                    {
                        m_cpuRate = (int)(rate.CpuRate > 0 ? rate.CpuRate / 100 : 0);
                    }
                    if ((rate.ControlFlags & JOB_OBJECT_CPU_RATE_CONTROL_MIN_MAX_RATE) != 0)
                    {
                        m_minMaxRate = new JobMinMaxValue(
                            (int)(rate.MinRate > 0 ? rate.MinRate / 100 : 0),
                            (int)(rate.MaxRate > 0 ? rate.MaxRate / 100 : 0));
                    }
                    if ((rate.ControlFlags & JOB_OBJECT_CPU_RATE_CONTROL_WEIGHT_BASED) != 0)
                    {
                        m_weight = (int)rate.Weight;
                    }
                }
            }
            finally
            {
                if (ratePtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(ratePtr);
                }
            }
        }

        private void QueryExtendedLimitInformation(JobAccountingInformation result, bool updateState = false)
        {
            IntPtr extendedLimitPtr = IntPtr.Zero;

            try
            {
                int extendedLimitSize = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                extendedLimitPtr = Marshal.AllocHGlobal(extendedLimitSize);
                bool success = QueryInformationJobObject(handle, JobObjectInfoType.ExtendedLimitInformation, extendedLimitPtr, (uint)extendedLimitSize, IntPtr.Zero);
                if (!success)
                {
                    throw GetException(Marshal.GetLastWin32Error(), "QueryInformationJobObject failed");
                }

                var extendedLimit = Marshal.PtrToStructure<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>(extendedLimitPtr);

                if (result != null)
                {
                    result.PeakJobMemory = extendedLimit.PeakJobMemoryUsed.ToInt64();
                    result.PeakProcessMemory = extendedLimit.PeakProcessMemoryUsed.ToInt64();
                }

                if (updateState)
                {
                    KillProcessesOnJobClose = false;
                    DieOnUnhandledException = false;
                    AllowChildProcessesBreakaway = false;
                    AlwaysBreakawayChildProcesses = false;
                    m_activeProcessesLimit = 0;
                    JobMemoryLimit = 0;
                    ProcessMemoryLimit = 0;
                    m_jobUserTimeLimitChanged = false;
                    m_jobUserTimeLimit = 0;
                    m_processUserTimeLimit = 0;
                    m_priorityClass = 0;
                    m_schedulingClass = 0;
                    ProcessorAffinity = IntPtr.Zero;
                    m_workingSetLimit = JobMinMaxValue.Empty;

                    if ((extendedLimit.BasicLimitInformation.LimitFlags & JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE) != 0)
                    {
                        KillProcessesOnJobClose = true;
                    }

                    if ((extendedLimit.BasicLimitInformation.LimitFlags & JOB_OBJECT_LIMIT_DIE_ON_UNHANDLED_EXCEPTION) != 0)
                    {
                        DieOnUnhandledException = true;
                    }

                    if ((extendedLimit.BasicLimitInformation.LimitFlags & JOB_OBJECT_LIMIT_BREAKAWAY_OK) != 0)
                    {
                        AllowChildProcessesBreakaway = true;
                    }

                    if ((extendedLimit.BasicLimitInformation.LimitFlags & JOB_OBJECT_LIMIT_SILENT_BREAKAWAY_OK) != 0)
                    {
                        AlwaysBreakawayChildProcesses = true;
                    }

                    if ((extendedLimit.BasicLimitInformation.LimitFlags & JOB_OBJECT_LIMIT_ACTIVE_PROCESS) != 0)
                    {
                        m_activeProcessesLimit = extendedLimit.BasicLimitInformation.ActiveProcessLimit;
                    }

                    if ((extendedLimit.BasicLimitInformation.LimitFlags & JOB_OBJECT_LIMIT_PROCESS_MEMORY) != 0)
                    {
                        ProcessMemoryLimit = (long)extendedLimit.ProcessMemoryLimit;
                    }

                    if ((extendedLimit.BasicLimitInformation.LimitFlags & JOB_OBJECT_LIMIT_JOB_MEMORY) != 0)
                    {
                        JobMemoryLimit = (long)extendedLimit.JobMemoryLimit;
                    }

                    if ((extendedLimit.BasicLimitInformation.LimitFlags & JOB_OBJECT_LIMIT_PROCESS_TIME) != 0)
                    {
                        m_processUserTimeLimit = extendedLimit.BasicLimitInformation.PerProcessUserTimeLimit;
                    }

                    if ((extendedLimit.BasicLimitInformation.LimitFlags & JOB_OBJECT_LIMIT_JOB_TIME) != 0)
                    {
                        m_jobUserTimeLimit = extendedLimit.BasicLimitInformation.PerJobUserTimeLimit;
                    }

                    if ((extendedLimit.BasicLimitInformation.LimitFlags & JOB_OBJECT_LIMIT_PRIORITY_CLASS) != 0)
                    {
                        m_priorityClass = extendedLimit.BasicLimitInformation.PriorityClass;
                    }

                    if ((extendedLimit.BasicLimitInformation.LimitFlags & JOB_OBJECT_LIMIT_SCHEDULING_CLASS) != 0)
                    {
                        m_schedulingClass = extendedLimit.BasicLimitInformation.SchedulingClass;
                    }

                    if ((extendedLimit.BasicLimitInformation.LimitFlags & JOB_OBJECT_LIMIT_AFFINITY) != 0)
                    {
                        ProcessorAffinity = extendedLimit.BasicLimitInformation.Affinity;
                    }

                    if ((extendedLimit.BasicLimitInformation.LimitFlags & JOB_OBJECT_LIMIT_WORKINGSET) != 0)
                    {
                        m_workingSetLimit = new JobMinMaxValue(
                            (long)extendedLimit.BasicLimitInformation.MinimumWorkingSetSize.ToUInt64(),
                            (long)extendedLimit.BasicLimitInformation.MaximumWorkingSetSize.ToUInt64());
                    }
                }
            }
            finally
            {
                if (extendedLimitPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(extendedLimitPtr);
                }
            }
        }

        private void QueryBasicAndIoAccounting(JobAccountingInformation result)
        {
            IntPtr accountingPtr = IntPtr.Zero;

            try
            {
                int accountingLength = Marshal.SizeOf(typeof(JOBOBJECT_BASIC_AND_IO_ACCOUNTING_INFORMATION));
                accountingPtr = Marshal.AllocHGlobal(accountingLength);
                bool success = QueryInformationJobObject(handle, JobObjectInfoType.BasicAndIoAccountingInformation, accountingPtr, (uint)accountingLength, IntPtr.Zero);
                if (!success)
                {
                    throw GetException(Marshal.GetLastWin32Error(), "QueryInformationJobObject failed");
                }

                var accounting = Marshal.PtrToStructure<JOBOBJECT_BASIC_AND_IO_ACCOUNTING_INFORMATION>(accountingPtr);

                result.UserProcessorTime = new TimeSpan((long)accounting.BasicInfo.TotalUserTime);
                result.KernelProcessorTime = new TimeSpan((long)accounting.BasicInfo.TotalKernelTime);
                result.IOReadBytes = (long)accounting.IoInfo.ReadTransferCount;
                result.IOWriteBytes = (long)accounting.IoInfo.WriteTransferCount;
                result.IOOtherBytes = (long)accounting.IoInfo.OtherTransferCount;
                result.IOReadOperationsCount = (long)accounting.IoInfo.ReadOperationCount;
                result.IOWriteOperationsCount = (long)accounting.IoInfo.WriteOperationCount;
                result.IOOtherOperationsCount = (long)accounting.IoInfo.OtherOperationCount;
            }
            finally
            {
                if (accountingPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(accountingPtr);
                }
            }
        }

        private List<int> QueryProcessIds()
        {
            var processList = new JOBOBJECT_BASIC_PROCESS_ID_LIST
            {
                NumberOfAssignedProcesses = JOBOBJECT_BASIC_PROCESS_ID_LIST.MaxProcessListLength,
                NumberOfProcessIdsInList = 0,
                ProcessIdList = null
            };

            IntPtr processListPtr = IntPtr.Zero;
            var result = new List<int>();
            try
            {
                int processListLength = Marshal.SizeOf(typeof(JOBOBJECT_BASIC_PROCESS_ID_LIST));
                processListPtr = Marshal.AllocHGlobal(processListLength);
                Marshal.StructureToPtr(processList, processListPtr, false);

                bool success = QueryInformationJobObject(handle, JobObjectInfoType.BasicProcessIdList, processListPtr, (uint)processListLength, IntPtr.Zero);
                if (!success)
                {
                    throw GetException(Marshal.GetLastWin32Error(), "QueryInformationJobObject failed.");
                }

                processList = Marshal.PtrToStructure<JOBOBJECT_BASIC_PROCESS_ID_LIST>(processListPtr);

                for (int i = 0; i < processList.NumberOfProcessIdsInList; i++)
                {
                    result.Add((int)processList.ProcessIdList[i]);
                }
            }
            finally
            {
                if (processListPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(processListPtr);
                }
            }

            return result;
        }

        private List<JobObjectLimitViolation> QueryLimitViolations()
        {
            var result = new List<JobObjectLimitViolation>();
            IntPtr ptr = IntPtr.Zero;
            try
            {
                int size = Marshal.SizeOf(typeof(JOBOBJECT_LIMIT_VIOLATION_INFORMATION));
                ptr = Marshal.AllocHGlobal(size);

                if (!QueryInformationJobObject(handle, JobObjectInfoType.BasicLimitInformation, ptr, (uint)size, IntPtr.Zero))
                {
                    throw GetException(Marshal.GetLastWin32Error(), "QueryInformationJobObject failed.");
                }

                var data = Marshal.PtrToStructure<JOBOBJECT_LIMIT_VIOLATION_INFORMATION>(ptr);

                if ((data.LimitFlags & JOB_OBJECT_LIMIT_JOB_MEMORY) != 0 &&
                    (data.ViolationLimitFlags & JOB_OBJECT_LIMIT_JOB_MEMORY) != 0)
                {
                    result.Add(new JobObjectJobMemoryLimitViolation(data.JobMemory, data.JobMemoryLimit));
                }

                if ((data.LimitFlags & JOB_OBJECT_LIMIT_JOB_TIME) != 0 &&
                    (data.ViolationLimitFlags & JOB_OBJECT_LIMIT_JOB_TIME) != 0)
                {
                    result.Add(new JobObjectJobUserTimeLimitViolation(
                        new TimeSpan((long)data.PerJobUserTime),
                        new TimeSpan((long)data.PerJobUserTimeLimit)));
                }

                if ((data.LimitFlags & JOB_OBJECT_LIMIT_JOB_READ_BYTES) != 0 &&
                    (data.ViolationLimitFlags & JOB_OBJECT_LIMIT_JOB_READ_BYTES) != 0)
                {
                    result.Add(new JobObjectReadBytesLimitViolation(data.IoReadBytes, data.IoReadBytesLimit));
                }

                if ((data.LimitFlags & JOB_OBJECT_LIMIT_JOB_WRITE_BYTES) != 0 &&
                    (data.ViolationLimitFlags & JOB_OBJECT_LIMIT_JOB_WRITE_BYTES) != 0)
                {
                    result.Add(new JobObjectWriteBytesLimitViolation(data.IoWriteBytes, data.IoWriteBytesLimit));
                }

                if ((data.LimitFlags & JOB_OBJECT_LIMIT_RATE_CONTROL) != 0 &&
                    (data.ViolationLimitFlags & JOB_OBJECT_LIMIT_RATE_CONTROL) != 0)
                {
                    var violation = new JobObjectRateLimitViolation();
                    switch (data.RateControlTolerance)
                    {
                        case JobRateControlTolerance.ToleranceLow:
                            violation.TolerancePercent = 20;
                            break;
                        case JobRateControlTolerance.ToleranceMedium:
                            violation.TolerancePercent = 40;
                            break;
                        default: // Per MSDN 60% is the default
                        case JobRateControlTolerance.ToleranceHigh:
                            violation.TolerancePercent = 60;
                            break;
                    }

                    switch (data.RateControlToleranceLimit)
                    {
                        // Per MSDN short is the default
                        default:
                        case JobRateControlToleranceLimit.ToleranceIntervalShort:
                            violation.ToleranceInterval = TimeSpan.FromSeconds(10);
                            break;
                        case JobRateControlToleranceLimit.ToleranceIntervalMedium:
                            violation.ToleranceInterval = TimeSpan.FromSeconds(60);
                            break;
                        case JobRateControlToleranceLimit.ToleranceIntervalLong:
                            violation.ToleranceInterval = TimeSpan.FromMinutes(10);
                            break;
                    }

                    result.Add(violation);
                }
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }

            return result;
        }

        // -----------------------------------------------------------------------------------------------

        private static IntPtr AcquireJobObject(string name, out bool created)
        {
            var result = CreateJobObject(IntPtr.Zero, name);
            int error = Marshal.GetLastWin32Error();
            if (result == IntPtr.Zero)
            {
                if (name != null)
                {
                    if (error == ERROR_INVALID_HANDLE)
                    {
                        throw GetException(error,
                            $"Failed to create job object with name '{name}'. " +
                            "Another object with that name already exists");
                    }

                    if (error == ERROR_ALREADY_EXISTS)
                    {
                        result = OpenJobObject(JOB_OBJECT_ALL_ACCESS, false, name);
                        if (result == IntPtr.Zero)
                        {
                            throw GetException(Marshal.GetLastWin32Error(),
                                $"Failed to open existing job object with name '{name}'");

                        }

                        created = false;
                        return result;
                    }
                }

                if (name != null)
                {
                    throw GetException(error,
                        $"Failed to create job object with name '{name}'");
                }

                throw GetException(error, "Failed to create job object");
            }

            created = error != ERROR_ALREADY_EXISTS;
            return result;
        }

        // -----------------------------------------------------------------------------------------------
        // Events
        // -----------------------------------------------------------------------------------------------

        private void EnsureRaisingEvents()
        {
            if (m_enableRaisingEvents)
                return;

            m_completionPort = CreateIoCompletionPort(new IntPtr(-1), IntPtr.Zero, IntPtr.Zero, 1);
            if (m_completionPort == IntPtr.Zero)
            {
                throw GetException(Marshal.GetLastWin32Error(), "Failed to create I/O completion port");
            }

            var assocInfo = new JOBOBJECT_ASSOCIATE_COMPLETION_PORT
            {
                CompletionKey = IntPtr.Zero,
                CompletionPort = m_completionPort
            };

            uint size = (uint)Marshal.SizeOf(assocInfo);
            if (!SetInformationJobObject(handle, JobObjectInfoType.AssociateCompletionPortInformation, ref assocInfo, size))
            {
                throw GetException(Marshal.GetLastWin32Error(), "Failed to set information on job");
            }

            m_eventThreadExit = false;
            m_eventThread = new Thread(EventWaiter);
            m_eventThread.Start();
        }

        private void EventWaiter()
        {
            try
            {
                while (!m_eventThreadExit)
                {
                    if (!GetQueuedCompletionStatus(m_completionPort, out uint msgIdentifier, out IntPtr _, out IntPtr lpOverlapped, 100))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (lpOverlapped == IntPtr.Zero && error != ERROR_ABANDONED_WAIT_0)
                        {
                            // Not data within timeout.
                            continue;
                        }

                        throw GetException(error, "Internal error waiting for I/O port");
                    }

                    JobObjectEventArgs args;
                    switch (msgIdentifier)
                    {
                        case JOB_OBJECT_MSG_NEW_PROCESS:
                            args = new JobObjectEventArgs(JobObjectEventType.NewProcess, (int)lpOverlapped, false, (int)msgIdentifier);
                            break;
                        case JOB_OBJECT_MSG_EXIT_PROCESS:
                            args = new JobObjectEventArgs(JobObjectEventType.ExitProcess, (int)lpOverlapped, false, (int)msgIdentifier);
                            break;
                        case JOB_OBJECT_MSG_ABNORMAL_EXIT_PROCESS:
                            args = new JobObjectEventArgs(JobObjectEventType.AbnormalExitProcess, (int)lpOverlapped, false, (int)msgIdentifier);
                            break;
                        case JOB_OBJECT_MSG_ACTIVE_PROCESS_ZERO:
                            args = new JobObjectEventArgs(JobObjectEventType.ActiveProcessZero, null, true, (int)msgIdentifier);
                            break;
                        case JOB_OBJECT_MSG_ACTIVE_PROCESS_LIMIT:
                            args = new JobObjectEventArgs(JobObjectEventType.ActiveProcessLimit, null, true, (int)msgIdentifier);
                            break;
                        case JOB_OBJECT_MSG_PROCESS_MEMORY_LIMIT:
                            args = new JobObjectEventArgs(JobObjectEventType.ProcessMemoryLimit, (int)lpOverlapped, false, (int)msgIdentifier);
                            break;
                        case JOB_OBJECT_MSG_JOB_MEMORY_LIMIT:
                            args = new JobObjectEventArgs(JobObjectEventType.JobMemoryLimit, (int)lpOverlapped, false, (int)msgIdentifier);
                            break;
                        case JOB_OBJECT_MSG_END_OF_PROCESS_TIME:
                            args = new JobObjectEventArgs(JobObjectEventType.EndOfProcessTime, (int)lpOverlapped,
                                !AlwaysBreakawayChildProcesses, (int)msgIdentifier); // EXIT when single process - we hit the process user-time limit
                            break;
                        case JOB_OBJECT_MSG_END_OF_JOB_TIME:
                            args = new JobObjectEventArgs(JobObjectEventType.EndOfJobTime, null, true, (int)msgIdentifier);
                            break;
                        case JOB_OBJECT_MSG_NOTIFICATION_LIMIT:
                            args = new JobObjectEventArgs(JobObjectEventType.NotificationLimit, null, true, (int)msgIdentifier)
                            {
                                LimitViolations = QueryLimitViolations()
                            };
                            break;
                        default:
                            args = new JobObjectEventArgs(JobObjectEventType.Unknown, (int)lpOverlapped, false, (int)msgIdentifier);
                            break;
                    }

                    EventRaised?.Invoke(this, args);
                    m_eventThreadExit = args.JobEnded;
                }
            }
            catch (Exception ex)
            {
                EventRaised?.Invoke(this, new JobObjectEventArgs(JobObjectEventType.InternalError,
                    null, false, -1, ex));
            }
        }

        private void StopRaisingEvents()
        {
            if (!m_enableRaisingEvents)
                return;

            StopEventThread();
            ResetCompletionPortState();
            CloseCompletionPort();
        }

        private void ResetCompletionPortState()
        {
            uint size = (uint)Marshal.SizeOf(typeof(JOBOBJECT_ASSOCIATE_COMPLETION_PORT));
            var assocInfo = new JOBOBJECT_ASSOCIATE_COMPLETION_PORT();
            if (!SetInformationJobObject(handle, JobObjectInfoType.AssociateCompletionPortInformation, ref assocInfo, size))
            {
                throw GetException(Marshal.GetLastWin32Error(), "Failed to set information on job");
            }
        }

        private void CloseCompletionPort()
        {
            if (m_completionPort != IntPtr.Zero)
            {
                CloseHandle(m_completionPort);
                m_completionPort = IntPtr.Zero;
            }
        }

        private void StopEventThread()
        {
            if (m_eventThread != null)
            {
                m_eventThreadExit = true;
                m_eventThread.Join();
                m_eventThread = null;
            }
        }

        private static Win32Exception GetException(int error, string message)
        {
            if (error == ERROR_ACCESS_DENIED)
            {
                throw new UnauthorizedAccessException(message);
            }

            var reason = new Win32Exception(error);
            return new Win32Exception(error, message + $" ({error:X8}: " + reason + ")");
        }

        #region NativeMethods

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr a, string lpName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr OpenJobObject(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoType infoType, IntPtr lpJobObjectInfo, UInt32 cbJobObjectInfoLength);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoType infoType, ref JOBOBJECT_ASSOCIATE_COMPLETION_PORT lpJobObjectInfo, UInt32 cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool QueryInformationJobObject(IntPtr hJob, JobObjectInfoType infoType, IntPtr lpJobObjectInfo, UInt32 cbJobObjectInfoLength, IntPtr lpReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool QueryInformationJobObject(IntPtr hJob, JobObjectInfoType infoType, out JOBOBJECT_ASSOCIATE_COMPLETION_PORT lpJobObjectInfo, UInt32 cbJobObjectInfoLength, IntPtr lpReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsProcessInJob(IntPtr ProcessHandle, IntPtr JobHandle, [MarshalAs(UnmanagedType.Bool)] out bool Result);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateJobObject(IntPtr hJob, uint uExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateIoCompletionPort(IntPtr FileHandle, IntPtr ExistingCompletionPort,
            IntPtr CompletionKey, uint NumberOfConcurrentThreads);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetQueuedCompletionStatus(IntPtr CompletionPort, out uint lpNumberOfBytes,
            out IntPtr lpCompletionKey, out IntPtr lpOverlapped, uint dwMilliseconds);

        // ReSharper disable InconsistentNaming
        // ReSharper disable FieldCanBeMadeReadOnly.Local
        // ReSharper disable MemberCanBePrivate.Local

        private const int ERROR_ACCESS_DENIED = 5;
        private const int ERROR_INVALID_HANDLE = 6;
        private const int ERROR_ALREADY_EXISTS = 183;
        private const int ERROR_ABANDONED_WAIT_0 = 735;

        private const uint JOB_OBJECT_ALL_ACCESS = 0x1F001F;

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public UInt64 ReadOperationCount;
            public UInt64 WriteOperationCount;
            public UInt64 OtherOperationCount;
            public UInt64 ReadTransferCount;
            public UInt64 WriteTransferCount;
            public UInt64 OtherTransferCount;
        }


        private const uint JOB_OBJECT_LIMIT_ACTIVE_PROCESS = 0x00000008;
        private const uint JOB_OBJECT_LIMIT_AFFINITY = 0x00000010;
        private const uint JOB_OBJECT_LIMIT_BREAKAWAY_OK = 0x00000800;
        private const uint JOB_OBJECT_LIMIT_DIE_ON_UNHANDLED_EXCEPTION = 0x00000400;
        private const uint JOB_OBJECT_LIMIT_JOB_MEMORY = 0x00000200;
        private const uint JOB_OBJECT_LIMIT_JOB_TIME = 0x00000004;
        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;
        private const uint JOB_OBJECT_LIMIT_PRESERVE_JOB_TIME = 0x00000040;
        private const uint JOB_OBJECT_LIMIT_PRIORITY_CLASS = 0x00000020;
        private const uint JOB_OBJECT_LIMIT_PROCESS_MEMORY = 0x00000100;
        private const uint JOB_OBJECT_LIMIT_PROCESS_TIME = 0x00000002;
        private const uint JOB_OBJECT_LIMIT_SCHEDULING_CLASS = 0x00000080;
        private const uint JOB_OBJECT_LIMIT_SILENT_BREAKAWAY_OK = 0x00001000;
        //private const uint JOB_OBJECT_LIMIT_SUBSET_AFFINITY = 0x00004000;
        private const uint JOB_OBJECT_LIMIT_WORKINGSET = 0x00000001;
        private const uint JOB_OBJECT_LIMIT_JOB_READ_BYTES = 0x00010000;
        private const uint JOB_OBJECT_LIMIT_JOB_WRITE_BYTES = 0x00020000;
        private const uint JOB_OBJECT_LIMIT_RATE_CONTROL = 0x00040000;

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public Int64 PerProcessUserTimeLimit;
            public Int64 PerJobUserTimeLimit;
            public UInt32 LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public UInt32 ActiveProcessLimit;
            public IntPtr Affinity;
            public UInt32 PriorityClass;
            public UInt32 SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_AND_IO_ACCOUNTING_INFORMATION
        {
            public JOBOBJECT_BASIC_ACCOUNTING_INFORMATION BasicInfo;
            public IO_COUNTERS IoInfo;
        }

        /// <summary>
        /// JOBOBJECT_BASIC_ACCOUNTING_INFORMATION Windows API structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_ACCOUNTING_INFORMATION
        {
            public ulong TotalUserTime;
            public ulong TotalKernelTime;
            public ulong ThisPeriodTotalUserTime;
            public ulong ThisPeriodTotalKernelTime;
            public uint TotalPageFaultCount;
            public uint TotalProcesses;
            public uint ActiveProcesses;
            public uint TotalTerminatedProcesses;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public IntPtr ProcessMemoryLimit;
            public IntPtr JobMemoryLimit;
            public IntPtr PeakProcessMemoryUsed;
            public IntPtr PeakJobMemoryUsed;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct JOBOBJECT_CPU_RATE_CONTROL_INFORMATION
        {
            [FieldOffset(0)]
            public int ControlFlags;
            [FieldOffset(4)]
            public int CpuRate;
            [FieldOffset(4)]
            public int Weight;
            [FieldOffset(4)]
            public short MinRate;
            [FieldOffset(6)]
            public short MaxRate;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_ASSOCIATE_COMPLETION_PORT
        {
            public IntPtr CompletionKey;
            public IntPtr CompletionPort;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_PROCESS_ID_LIST
        {
            public const uint MaxProcessListLength = 200;
            public uint NumberOfAssignedProcesses;
            public uint NumberOfProcessIdsInList;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)MaxProcessListLength)]
            public IntPtr[] ProcessIdList;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_LIMIT_VIOLATION_INFORMATION
        {
            internal int LimitFlags;
            internal int ViolationLimitFlags;

            internal ulong IoReadBytes;
            internal ulong IoReadBytesLimit;
            internal ulong IoWriteBytes;
            internal ulong IoWriteBytesLimit;
            internal ulong PerJobUserTime;
            internal ulong PerJobUserTimeLimit;
            internal ulong JobMemory;
            internal ulong JobMemoryLimit;
            internal JobRateControlTolerance RateControlTolerance;
            internal JobRateControlToleranceLimit RateControlToleranceLimit;
        }



        private enum JobRateControlTolerance
        {
            /// <summary>The job exceeded its CPU rate control limits for 20% of the tolerance interval.</summary>
            ToleranceLow = 1,
            /// <summary>The job exceeded its CPU rate control limits for 40% of the tolerance interval.</summary>
            ToleranceMedium = 2,
            /// <summary>The job exceeded its CPU rate control limits for 50% of the tolerance interval.</summary>
            ToleranceHigh = 3
        }

        private enum JobRateControlToleranceLimit
        {
            /// <summary>The tolerance interval is 10 seconds.</summary>
            ToleranceIntervalShort = 1,
            /// <summary>The tolerance interval is 60 seconds.</summary>
            ToleranceIntervalMedium = 2,
            /// <summary>The tolerance interval is 10 minutes.</summary>
            ToleranceIntervalLong = 3
        }

        private const int JOB_OBJECT_CPU_RATE_CONTROL_ENABLE = 0x00000001;
        private const int JOB_OBJECT_CPU_RATE_CONTROL_WEIGHT_BASED = 0x00000002;
        private const int JOB_OBJECT_CPU_RATE_CONTROL_HARD_CAP = 0x00000004;
        private const int JOB_OBJECT_CPU_RATE_CONTROL_NOTIFY = 0x00000008;
        private const int JOB_OBJECT_CPU_RATE_CONTROL_MIN_MAX_RATE = 0x00000010;

        private const uint JOB_OBJECT_MSG_END_OF_JOB_TIME = 1;
        private const uint JOB_OBJECT_MSG_END_OF_PROCESS_TIME = 2;
        private const uint JOB_OBJECT_MSG_ACTIVE_PROCESS_LIMIT = 3;
        private const uint JOB_OBJECT_MSG_ACTIVE_PROCESS_ZERO = 4;
        private const uint JOB_OBJECT_MSG_NEW_PROCESS = 6;
        private const uint JOB_OBJECT_MSG_EXIT_PROCESS = 7;
        private const uint JOB_OBJECT_MSG_ABNORMAL_EXIT_PROCESS = 8;
        private const uint JOB_OBJECT_MSG_PROCESS_MEMORY_LIMIT = 9;
        private const uint JOB_OBJECT_MSG_JOB_MEMORY_LIMIT = 10;
        private const uint JOB_OBJECT_MSG_NOTIFICATION_LIMIT = 11;

        private enum JobObjectInfoType
        {
            BasicAccountingInformation = 1,
            BasicLimitInformation = 2,
            BasicProcessIdList = 3,
            BasicUIRestrictions = 4,
            SecurityLimitInformation = 5,
            EndOfJobTimeInformation = 6,
            AssociateCompletionPortInformation = 7,
            BasicAndIoAccountingInformation = 8,
            ExtendedLimitInformation = 9,
            JobSetInformation = 10,
            GroupInformation = 11,
            CpuRateControlInformation = 15
            //MaxJobObjectInfoClass
        }
        // ReSharper restore InconsistentNaming
        // ReSharper restore MemberCanBePrivate.Local
        // ReSharper restore FieldCanBeMadeReadOnly.Local
        #endregion
    }
}
