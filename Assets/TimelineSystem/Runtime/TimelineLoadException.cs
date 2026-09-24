using System;

namespace TimelineSystem
{
    public sealed class TimelineLoadException : Exception
    {
        public TimelineLoadException(string message)
            : base(message)
        {
        }

        public TimelineLoadException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
