using System;

namespace TimelineSystem
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class TimelineCommandAttribute : Attribute
    {
        public string CommandId { get; }

        public TimelineCommandAttribute(string commandId)
        {
            CommandId = commandId;
        }
    }
}
