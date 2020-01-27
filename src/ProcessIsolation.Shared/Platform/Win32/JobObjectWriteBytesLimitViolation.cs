namespace ProcessIsolation.Shared.Platform.Win32
{
    public class JobObjectWriteBytesLimitViolation : JobObjectValueLimitViolation
    {
        internal JobObjectWriteBytesLimitViolation(ulong currentValue, ulong limitValue)
            : base(currentValue, limitValue)
        {
        }

        public override string ToString()
        {
            return $"WriteBytes: Value={CurrentValue:N0}, Limit={LimitValue:N0}";
        }

    }
}
