using System;

namespace ProcessIsolation.Shared.Platform.Win32
{
    public struct JobMinMaxValue : IEquatable<JobMinMaxValue>
    {
        public static readonly JobMinMaxValue Empty = new JobMinMaxValue(0, 0);

        public JobMinMaxValue(long minimum, long maximum)
        {
            Minimum = minimum;
            Maximum = maximum;
        }

        public long Minimum { get; }
        public long Maximum { get; }

        public bool Equals(JobMinMaxValue other)
        {
            return Minimum == other.Minimum && Maximum == other.Maximum;
        }

        public override bool Equals(object obj)
        {
            return obj is JobMinMaxValue other && Equals(other);
        }

        public override int GetHashCode()
        {
            int h1 = Minimum.GetHashCode();
            int h2 = Maximum.GetHashCode();
            return ((h1 << 5) + h1) ^ h2;
        }

        public override string ToString()
        {
            return Minimum + "/" + Maximum;
        }

        public static bool operator ==(JobMinMaxValue left, JobMinMaxValue right)
        {
            return left.Minimum == right.Minimum &&
                   left.Maximum == right.Maximum;
        }

        public static bool operator !=(JobMinMaxValue left, JobMinMaxValue right)
        {
            return left.Minimum != right.Minimum ||
                   left.Maximum != right.Maximum;
        }
    }
}
