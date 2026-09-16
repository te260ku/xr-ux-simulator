using System;
using System.Collections.Generic;

namespace App.Timeline
{
    public sealed class TimelineCommandSchema
    {
        public string CommandId { get; }
        public IReadOnlyList<TimelineParameterSchema> Parameters { get; }

        public TimelineCommandSchema(
            string commandId,
            IReadOnlyList<TimelineParameterSchema> parameters)
        {
            if (string.IsNullOrWhiteSpace(commandId))
                throw new ArgumentException("Command ID must not be empty.", nameof(commandId));

            CommandId = commandId;
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        }
    }
}
