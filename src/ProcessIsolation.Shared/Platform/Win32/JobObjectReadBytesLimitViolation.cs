namespace ProcessIsolation.Shared.Platform.Win32
{
    public class JobObjectReadBytesLimitViolation : JobObjectValueLimitViolation
    {
        public JobObjectReadBytesLimitViolation(ulong currentValue, ulong limitValue)
            : base(currentValue, limitValue)
        {
        }

        public override string ToString()
        {
            return $"ReadBytes: Value={CurrentValue:N0}, Limit={LimitValue:N0}";
        }
    }
}
