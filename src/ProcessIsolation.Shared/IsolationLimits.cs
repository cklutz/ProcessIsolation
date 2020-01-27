using System;
using System.Globalization;
using ProcessIsolation.Shared.Platform;

namespace ProcessIsolation.Shared
{
    public class IsolationLimits
    {
        public IsolationLimits()
        {
        }

        public IsolationLimits(long maxMemory, int maxCpuUsage, IntPtr affinityMask)
        {
            if (maxMemory < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxMemory), maxMemory, null);
            }

            if (maxCpuUsage < 0 || maxCpuUsage > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCpuUsage), maxCpuUsage, null);
            }

            MaxMemory = maxMemory;
            MaxCpuUsage = maxCpuUsage;
            AffinityMask = affinityMask;
        }

        public IsolationLimits(long maxMemory, int maxCpuUsage, int affinityMask)
            : this(maxMemory, maxCpuUsage, new IntPtr(affinityMask))
        {
        }

        public long MaxMemory { get; }
        public int MaxCpuUsage { get; }
        public IntPtr AffinityMask { get; }

        public IsolationLimits WithMaxMemory(string bytes, CultureInfo cultureInfo = null)
        {
            return new IsolationLimits(Utils.ParseBytes(bytes, cultureInfo), MaxCpuUsage, AffinityMask);
        }

        public IsolationLimits WithMaxCpu(string value, CultureInfo cultureInfo = null)
        {
            return new IsolationLimits(MaxMemory, int.Parse(value, cultureInfo), AffinityMask);
        }

        public IsolationLimits WithAffinityMask(string value, CultureInfo cultureInfo = null)
        {
            return new IsolationLimits(MaxMemory, MaxCpuUsage, ProcessAffinity.Parse(value));
        }

        public bool IsAnyEnabled => MaxMemory > 0 || MaxCpuUsage > 0 || AffinityMask != IntPtr.Zero;
    }
}
