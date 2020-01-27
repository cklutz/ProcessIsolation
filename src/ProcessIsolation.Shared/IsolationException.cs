using System;

namespace ProcessIsolation.Shared
{
    public class IsolationException : Exception
    {
        public IsolationException()
        {
        }

        public IsolationException(string message)
            : base(message)
        {
        }

        public IsolationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
