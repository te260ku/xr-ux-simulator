using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace App.Timeline
{
    public sealed class TimelineScenarioLoader
    {
        private readonly ICsvReader csvReader;
        private readonly TimelineCommandArgumentBinder binder;

        public TimelineScenarioLoader(
            ICsvReader csvReader,
            TimelineCommandArgumentBinder binder)
        {
            this.csvReader = csvReader ?? throw new ArgumentNullException(nameof(csvReader));
            this.binder = binder ?? throw new ArgumentNullException(nameof(binder));
        }

        public TimelineScenario Load(string csvText)
        {
            var table = csvReader.Read(csvText);
            if (table.Count == 0)
                throw new FormatException("Timeline CSV is empty.");

            var headerCells = table[0]
                .Select((value, index) => new { Name = value.Trim(), Index = index })
                .ToArray();

            var timeColumn = headerCells.FirstOrDefault(x =>
                string.Equals(x.Name, "Time", StringComparison.OrdinalIgnoreCase));
            var commandColumn = headerCells.FirstOrDefault(x =>
                string.Equals(x.Name, "Command", StringComparison.OrdinalIgnoreCase));

            if (timeColumn == null)
                throw new FormatException("Timeline CSV requires a 'Time' column.");
            if (commandColumn == null)
                throw new FormatException("Timeline CSV requires a 'Command' column.");

            var argColumns = headerCells
                .Where(x => x.Name.StartsWith("Arg", StringComparison.OrdinalIgnoreCase))
                .Select(x => new
                {
                    x.Index,
                    Number = int.TryParse(x.Name.Substring(3), out var n) ? n : -1
                })
                .Where(x => x.Number > 0)
                .OrderBy(x => x.Number)
                .ToArray();

            for (var i = 0; i < argColumns.Length; i++)
            {
                if (argColumns[i].Number != i + 1)
                    throw new FormatException("Timeline CSV Arg columns must be contiguous: Arg1, Arg2, Arg3, ...");
            }

            var parsedRows = new List<TimelineCsvRow>();
            for (var rowIndex = 1; rowIndex < table.Count; rowIndex++)
            {
                var values = table[rowIndex];
                var sourceRowNumber = rowIndex + 1;

                var timeText = Get(values, timeColumn.Index).Trim();
                if (!double.TryParse(timeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var time) ||
                    double.IsNaN(time) || double.IsInfinity(time) || time < 0)
                {
                    throw new FormatException(
                        $"Timeline CSV row {sourceRowNumber}: Time '{timeText}' must be a finite number greater than or equal to zero.");
                }

                var commandId = Get(values, commandColumn.Index).Trim();
                if (commandId.Length == 0)
                    throw new FormatException($"Timeline CSV row {sourceRowNumber}: Command is required.");

                var rawArguments = argColumns
                    .Select(column => Get(values, column.Index))
                    .ToArray();

                parsedRows.Add(new TimelineCsvRow(
                    sourceRowNumber,
                    time,
                    commandId,
                    rawArguments));
            }

            var events = new List<(TimelineEvent Event, int SourceOrder)>();
            foreach (var row in parsedRows)
            {
                try
                {
                    var invocation = binder.Bind(row.CommandId, row.RawArguments);
                    events.Add((
                        new TimelineEvent(row.Time, invocation),
                        row.SourceRowNumber));
                }
                catch (Exception exception) when (!(exception is OutOfMemoryException))
                {
                    throw new FormatException(
                        $"Timeline CSV row {row.SourceRowNumber} is invalid: {exception.Message}",
                        exception);
                }
            }

            var ordered = events
                .OrderBy(x => x.Event.Time)
                .ThenBy(x => x.SourceOrder)
                .Select(x => x.Event)
                .ToArray();

            return new TimelineScenario(ordered);
        }

        private static string Get(IReadOnlyList<string> row, int index)
        {
            return index >= 0 && index < row.Count ? row[index] ?? string.Empty : string.Empty;
        }
    }
}
