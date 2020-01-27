using Microsoft.Extensions.Logging;

namespace ProcessIsolation.Host
{
    internal static class EventIds
    {
        private const int Base = 0;

        public static readonly EventId HostProcessStart = new EventId(Base + 1, nameof(HostProcessStart));
        public static readonly EventId ProcessLimitSet = new EventId(Base + 2, nameof(ProcessLimitSet));

        public static readonly EventId IpcServiceHostStarting = new EventId(Base + 3, nameof(IpcServiceHostStarting));
        public static readonly EventId InvokeMethodException = new EventId(Base + 4, nameof(InvokeMethodException));

        public static readonly EventId InvokeMethodInvalidSpecification = new EventId(Base + 5, nameof(InvokeMethodInvalidSpecification));
        public static readonly EventId InvokeMethodAssemblyLoadFailure = new EventId(Base + 6, nameof(InvokeMethodAssemblyLoadFailure));
        public static readonly EventId InvokeMethodTypeNotFound = new EventId(Base + 7, nameof(InvokeMethodTypeNotFound));
        public static readonly EventId InvokeMethodMethodNotFound = new EventId(Base + 8, nameof(InvokeMethodMethodNotFound));
    }
}
