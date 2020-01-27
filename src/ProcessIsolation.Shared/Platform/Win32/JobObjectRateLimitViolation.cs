using System;

namespace ProcessIsolation.Shared.Platform.Win32
{
    public class JobObjectRateLimitViolation : JobObjectLimitViolation
    {
        public int TolerancePercent { get; internal set; }
        public TimeSpan ToleranceInterval { get; internal set; }
        public TimeSpan TimeOutOfInterval => TimeSpan.FromTicks(TolerancePercent * ToleranceInterval.Ticks / 100);

        public override string ToString()
        {
            return $"RateControl: OutOfInterval={TimeOutOfInterval} ({TolerancePercent}% of {ToleranceInterval})";
        }
    }
}
