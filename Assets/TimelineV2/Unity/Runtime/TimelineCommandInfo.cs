using System;
using System.Reflection;

namespace App.Timeline.V2
{
    /// <summary>
    /// Runtime information required to invoke one timeline command.
    /// </summary>
    public sealed class TimelineCommandInfo
    {
        public string CommandId { get; }
        public object Target { get; }
        public MethodInfo Method { get; }

        public TimelineCommandInfo(string commandId, object target, MethodInfo method)
        {
            if (string.IsNullOrWhiteSpace(commandId))
                throw new ArgumentException("Command ID must not be empty.", nameof(commandId));

            CommandId = commandId;
            Target = target;
            Method = method ?? throw new ArgumentNullException(nameof(method));
        }
    }
}
