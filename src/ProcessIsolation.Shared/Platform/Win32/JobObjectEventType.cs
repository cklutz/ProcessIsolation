namespace ProcessIsolation.Shared.Platform.Win32
{
    public enum JobObjectEventType
    {
        Unknown,
        InternalError,
        EndOfJobTime,
        EndOfProcessTime,
        ActiveProcessLimit,
        ActiveProcessZero,
        NewProcess,
        ExitProcess,
        AbnormalExitProcess,
        ProcessMemoryLimit,
        JobMemoryLimit,
        NotificationLimit
    }
}
