using System;
using System.Collections.Generic;

namespace App.Timeline.V2
{
    /// <summary>
    /// One row of an already parsed timeline CSV.
    /// Arguments stay as strings until the command is invoked.
    /// </summary>
    public sealed class TimelineEvent
    {
        public int SourceRowNumber { get; }
        public double TimeSeconds { get; }
        public string CommandId { get; }
        public IReadOnlyList<string> Arguments { get; }

        public TimelineEvent(
            int sourceRowNumber,
            double timeSeconds,
            string commandId,
            IReadOnlyList<string> arguments)
        {
            if (sourceRowNumber < 1) throw new ArgumentOutOfRangeException(nameof(sourceRowNumber));
            if (timeSeconds < 0 || double.IsNaN(timeSeconds) || double.IsInfinity(timeSeconds))
                throw new ArgumentOutOfRangeException(nameof(timeSeconds));
            if (string.IsNullOrWhiteSpace(commandId))
                throw new ArgumentException("Command ID must not be empty.", nameof(commandId));

            SourceRowNumber = sourceRowNumber;
            TimeSeconds = timeSeconds;
            CommandId = commandId;
            Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
        }
    }
}
