using System.Collections.Generic;

namespace App.Timeline
{
    internal sealed class TimelineCsvRow
    {
        public int SourceRowNumber { get; }
        public double Time { get; }
        public string CommandId { get; }
        public IReadOnlyList<string> RawArguments { get; }

        public TimelineCsvRow(
            int sourceRowNumber,
            double time,
            string commandId,
            IReadOnlyList<string> rawArguments)
        {
            SourceRowNumber = sourceRowNumber;
            Time = time;
            CommandId = commandId;
            RawArguments = rawArguments;
        }
    }
}
