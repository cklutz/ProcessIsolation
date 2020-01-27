using System;

namespace ProcessIsolation.Shared
{
    public class IsolationMethodInvocationException : IsolationException
    {
        public IsolationMethodInvocationException()
        {
        }

        public IsolationMethodInvocationException(string message)
            : base(message)
        {
        }

        public IsolationMethodInvocationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public IsolationMethodInvocationException(Exception ex)
            : base("Method invocation failed", ex)
        {
        }
    }
}
