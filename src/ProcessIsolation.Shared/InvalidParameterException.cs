using System;

namespace ProcessIsolation.Shared
{
    public class InvalidParameterException : IsolationException
    {
        public InvalidParameterException()
        {
        }

        public InvalidParameterException(string message) : base(message)
        {
        }

        public InvalidParameterException(string message, Exception innerException) : base(message, innerException)
        {
        }


        public InvalidParameterException(string parameter, object value)
            : this(parameter, value, null)
        {
        }

        public InvalidParameterException(string parameter, object value, Exception ex)
            : base($"The value {value} for the parameter {parameter} is invalid", ex)
        {
        }
    }
}
