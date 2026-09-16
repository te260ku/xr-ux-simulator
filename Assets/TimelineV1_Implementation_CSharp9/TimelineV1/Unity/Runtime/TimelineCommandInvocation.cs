using System;
using System.Collections.Generic;

namespace App.Timeline
{
    public sealed class TimelineCommandInvocation
    {
        public string CommandId { get; }
        public IReadOnlyList<object> Arguments { get; }

        public TimelineCommandInvocation(
            string commandId,
            IReadOnlyList<object> arguments)
        {
            if (string.IsNullOrWhiteSpace(commandId))
                throw new ArgumentException("Command ID must not be empty.", nameof(commandId));

            CommandId = commandId;
            Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
        }
    }
}
