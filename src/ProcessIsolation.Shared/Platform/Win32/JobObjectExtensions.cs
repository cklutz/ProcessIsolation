using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace ProcessIsolation.Shared.Platform.Win32
{
    public static class JobObjectExtensions
    {
        public static JobObject Enable(this JobObject job, IsolationLimits limits, Action<string, object> onSet = null)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            if (limits == null)
            {
                throw new ArgumentNullException(nameof(limits));
            }

            job.Update(o =>
            {
                if (limits.MaxMemory > 0)
                {
                    o.ProcessMemoryLimit = limits.MaxMemory;
                    onSet?.Invoke(nameof(IsolationLimits.MaxMemory), limits.MaxMemory);
                }

                if (limits.MaxCpuUsage > 0)
                {
                    o.CpuRateLimit = limits.MaxCpuUsage;
                    onSet?.Invoke(nameof(IsolationLimits.MaxCpuUsage), limits.MaxCpuUsage);
                }

                if (limits.AffinityMask != IntPtr.Zero)
                {
                    o.ProcessorAffinity = limits.AffinityMask;
                    onSet?.Invoke(nameof(IsolationLimits.AffinityMask), limits.AffinityMask);
                }
            });

            return job;
        }

        public static void FormatProcesses(this IJobObjectInformation o, Action<string> output)
        {
            FormatProcesses(o, output, CultureInfo.CurrentCulture);
        }

        public static void FormatProcesses(this IJobObjectInformation o, Action<string> output, CultureInfo culture)
        {
            if (o == null)
            {
                throw new ArgumentNullException(nameof(o));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            int myPid = Process.GetCurrentProcess().Id;
            var now = DateTime.UtcNow;
            foreach (var processId in o.GetProcessIds().OrderBy(pid => pid))
            {
                var details = GetProcessDetails(processId, now, culture);
                output(string.Format(culture, "{0}{1}    {2,12}        {3,26}",
                    processId,
                    processId == myPid ? '*' : ' ',
                    details.Item1, details.Item2));
            }
        }

        public static void FormatValues(this IJobObjectInformation o, Action<string> output)
        {
            FormatValues(o, output, CultureInfo.CurrentCulture);
        }

        public static void FormatValues(this IJobObjectInformation o, Action<string> output, CultureInfo culture)
        {
            if (o == null)
            {
                throw new ArgumentNullException(nameof(o));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            var v = o.GetAccountingInformation();

            output(string.Format(culture, "KernelProcessorTime           {0,26}", v.KernelProcessorTime.ToString("c", culture)));
            output(string.Format(culture, "UserProcessorTime             {0,26}", v.UserProcessorTime.ToString("c", culture)));
            output(string.Format(culture, "TotalProcessorTime            {0,26}", v.TotalProcessorTime.ToString("c", culture)));
            output(string.Format(culture, "PeakProcessMemory             {0,26:N0}", v.PeakProcessMemory));
            output(string.Format(culture, "PeakJobMemory                 {0,26:N0}", v.PeakJobMemory));
            output(string.Format(culture, "IOReadBytes                   {0,26:N0}", v.IOReadBytes));
            output(string.Format(culture, "IOWriteBytes                  {0,26:N0}", v.IOWriteBytes));
            output(string.Format(culture, "IOWriteBytes                  {0,26:N0}", v.IOOtherBytes));
            output(string.Format(culture, "IOReadOperationsCount         {0,26:N0}", v.IOReadOperationsCount));
            output(string.Format(culture, "IOWriteOperationsCount        {0,26:N0}", v.IOWriteOperationsCount));
            output(string.Format(culture, "IOOtherOperationsCount        {0,26:N0}", v.IOOtherOperationsCount));
        }

        public static void FormatLimits(this IJobObjectInformation o, Action<string> output)
        {
            FormatLimits(o, output, CultureInfo.CurrentCulture);
        }

        public static void FormatLimits(this IJobObjectInformation o, Action<string> output, CultureInfo culture)
        {
            if (o == null)
            {
                throw new ArgumentNullException(nameof(o));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (o.KillProcessesOnJobClose)
            {
                output(string.Format(culture, "KillProcessesOnJobClose       {0,26}", o.KillProcessesOnJobClose));
            }

            if (o.CpuRateLimit > 0)
            {
                output(string.Format(culture, "CpuRateLimit                  {0,25}%", o.CpuRateLimit));
            }

            if (o.ProcessMemoryLimit > 0)
            {
                output(string.Format(culture, "ProcessMemoryLimit            {0,26}", Utils.FormatBytes(o.ProcessMemoryLimit)));
            }

            if (o.WorkingSetLimit != JobMinMaxValue.Empty)
            {
                output(string.Format(culture, "WorkingSetLimit               {0,26}",
                   Utils.FormatBytes(o.WorkingSetLimit.Minimum) + "/" +
                   Utils.FormatBytes(o.WorkingSetLimit.Maximum)));
            }

            if (o.JobMemoryLimit > 0)
            {
                output(string.Format(culture, "JobMemoryLimit                {0,26}", Utils.FormatBytes(o.JobMemoryLimit)));
            }

            if (o.AllowChildProcessesBreakaway)
            {
                output(string.Format(culture, "AllowChildProcessesBreakaway  {0,26}", o.AllowChildProcessesBreakaway));
            }

            if (o.AlwaysBreakawayChildProcesses)
            {
                output(string.Format(culture, "AlwaysBreakawayChildProcesses {0,26}", o.AlwaysBreakawayChildProcesses));
            }

            if (o.ActiveProcessesLimit > 0)
            {
                output(string.Format(culture, "ActiveProcessesLimit          {0,26}", o.ActiveProcessesLimit));
            }

            if (o.DieOnUnhandledException)
            {
                output(string.Format(culture, "DieOnUnhandledException       {0,26}", o.DieOnUnhandledException));
            }

            if (o.JobUserTimeLimit != TimeSpan.Zero)
            {
                output(string.Format(culture, "JobUserTimeLimit              {0,26}", o.JobUserTimeLimit));
            }

            if (o.ProcessUserTimeLimit != TimeSpan.Zero)
            {
                output(string.Format(culture, "ProcessUserTimeLimit          {0,26}", o.ProcessUserTimeLimit));
            }

            if (o.PriorityClass > 0)
            {
                output(string.Format(culture, "PriorityClass                 {0,26}", o.PriorityClass));
            }

            if (o.SchedulingClass > 0)
            {
                output(string.Format(culture, "SchedulingClass               {0,26}", o.SchedulingClass));
            }

            if (o.ProcessorAffinity != IntPtr.Zero)
            {
                output(string.Format(culture, "ProcessorAffinity             {0,26}", ProcessAffinity.ToString(o.ProcessorAffinity)));
            }

            if (o.Weight > 0)
            {
                output(string.Format(culture, "Weight                        {0,26}", o.Weight));
            }

            if (o.RateMinMaxLimit != JobMinMaxValue.Empty)
            {
                output(string.Format(culture, "RateMinMaxLimit               {0,26}", o.RateMinMaxLimit));
            }
        }

        private static Tuple<string, string> GetProcessDetails(int processId, DateTime now, CultureInfo cultureInfo)
        {
            try
            {
                using (var process = Process.GetProcessById(processId))
                {
                    return Tuple.Create((now - process.StartTime.ToUniversalTime()).ToString("c", cultureInfo), process.ProcessName);
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (Exception ex)
            {
                return Tuple.Create("", ex.Message);
            }

            return Tuple.Create("", "");
        }
    }
}
