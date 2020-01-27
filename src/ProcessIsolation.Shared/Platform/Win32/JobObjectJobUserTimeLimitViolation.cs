using System;

namespace ProcessIsolation.Shared.Platform.Win32
{
    public class JobObjectJobUserTimeLimitViolation : JobObjectLimitViolation
    {
        public JobObjectJobUserTimeLimitViolation(TimeSpan currentValue, TimeSpan limitValue)
        {
            CurrentValue = currentValue;
            LimitValue = limitValue;
        }

        public TimeSpan CurrentValue { get; }
        public TimeSpan LimitValue { get; }

        public override string ToString()
        {
            return $"JobUserTime: Value={CurrentValue}, Limit={LimitValue}";
        }
    }
}
