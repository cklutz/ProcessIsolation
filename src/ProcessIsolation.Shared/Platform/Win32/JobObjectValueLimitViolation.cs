namespace ProcessIsolation.Shared.Platform.Win32
{
    public abstract class JobObjectValueLimitViolation : JobObjectLimitViolation
    {
        protected JobObjectValueLimitViolation(ulong currentValue, ulong limitValue)
        {
            CurrentValue = currentValue;
            LimitValue = limitValue;
        }

        public ulong CurrentValue { get; internal set; }
        public ulong LimitValue { get; internal set; }
    }
}
