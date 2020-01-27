namespace ProcessIsolation.Shared.Platform.Win32
{
    public class JobObjectJobMemoryLimitViolation : JobObjectValueLimitViolation
    {
        public JobObjectJobMemoryLimitViolation(ulong currentValue, ulong limitValue)
            : base(currentValue, limitValue)
        {
        }

        public override string ToString()
        {
            return $"JobMemory: Value={CurrentValue:N0}, Limit={LimitValue:N0}";
        }
    }
}
