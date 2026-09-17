using System;
using System.Collections.Generic;

namespace App.Timeline.V2
{
    /// <summary>
    /// Represents another timeline command supplied as an argument to a command.
    /// The arguments are already converted to their C# types.
    /// </summary>
    public sealed class TimelineCommandCall
    {
        public string CommandId { get; }
        public IReadOnlyList<object> Arguments { get; }

        public TimelineCommandCall(string commandId, IReadOnlyList<object> arguments)
        {
            if (string.IsNullOrWhiteSpace(commandId))
                throw new ArgumentException("Command ID must not be empty.", nameof(commandId));

            CommandId = commandId;
            Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
        }
    }
}
