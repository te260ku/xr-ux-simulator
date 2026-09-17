using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace App.Timeline.V2
{
    /// <summary>
    /// Parses Time, Command, Arg1, Arg2, ... timeline CSV files.
    /// </summary>
    public sealed class TimelineCsvLoader
    {
        private readonly ICsvReader csvReader;
        private readonly TimelineCommandRegistry commandRegistry;

        public TimelineCsvLoader(ICsvReader csvReader, TimelineCommandRegistry commandRegistry)
        {
            this.csvReader = csvReader ?? throw new ArgumentNullException(nameof(csvReader));
            this.commandRegistry = commandRegistry ?? throw new ArgumentNullException(nameof(commandRegistry));
        }

        public TimelineScenario Load(string csvText)
        {
            var table = csvReader.Read(csvText);
            if (table.Count == 0)
                throw new FormatException("Timeline CSV is empty.");

            var headers = table[0]
                .Select((name, index) => new HeaderColumn(name.Trim(), index))
                .ToArray();

            var timeColumn = FindRequiredColumn(headers, "Time");
            var commandColumn = FindRequiredColumn(headers, "Command");
            var argumentColumns = FindArgumentColumns(headers);

            var events = new List<TimelineEvent>();

            for (var rowIndex = 1; rowIndex < table.Count; rowIndex++)
            {
                var sourceRowNumber = rowIndex + 1;
                var row = table[rowIndex];

                var timeText = GetCell(row, timeColumn.Index).Trim();
                if (!double.TryParse(timeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var timeSeconds) ||
                    timeSeconds < 0 || double.IsNaN(timeSeconds) || double.IsInfinity(timeSeconds))
                {
                    throw new FormatException(
                        $"Timeline CSV row {sourceRowNumber}: Time '{timeText}' is invalid.");
                }

                var commandId = GetCell(row, commandColumn.Index).Trim();
                if (commandId.Length == 0)
                    throw new FormatException($"Timeline CSV row {sourceRowNumber}: Command is required.");

                if (!commandRegistry.Contains(commandId))
                    throw new FormatException(
                        $"Timeline CSV row {sourceRowNumber}: Unknown command '{commandId}'.");

                var arguments = new string[argumentColumns.Count];
                for (var argumentIndex = 0; argumentIndex < argumentColumns.Count; argumentIndex++)
                {
                    arguments[argumentIndex] = GetCell(row, argumentColumns[argumentIndex].Index);
                }

                events.Add(new TimelineEvent(
                    sourceRowNumber,
                    timeSeconds,
                    commandId,
                    arguments));
            }

            var orderedEvents = events
                .OrderBy(timelineEvent => timelineEvent.TimeSeconds)
                .ThenBy(timelineEvent => timelineEvent.SourceRowNumber)
                .ToArray();

            return new TimelineScenario(orderedEvents);
        }

        private static HeaderColumn FindRequiredColumn(
            IReadOnlyList<HeaderColumn> headers,
            string columnName)
        {
            var column = headers.FirstOrDefault(header =>
                string.Equals(header.Name, columnName, StringComparison.OrdinalIgnoreCase));

            if (column == null)
                throw new FormatException($"Timeline CSV requires a '{columnName}' column.");

            return column;
        }

        private static IReadOnlyList<HeaderColumn> FindArgumentColumns(
            IReadOnlyList<HeaderColumn> headers)
        {
            var columns = new List<HeaderColumn>();

            foreach (var header in headers)
            {
                if (!header.Name.StartsWith("Arg", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!int.TryParse(header.Name.Substring(3), out var argumentNumber) || argumentNumber < 1)
                    continue;

                columns.Add(new HeaderColumn(header.Name, header.Index, argumentNumber));
            }

            columns.Sort((left, right) => left.ArgumentNumber.CompareTo(right.ArgumentNumber));

            for (var index = 0; index < columns.Count; index++)
            {
                if (columns[index].ArgumentNumber != index + 1)
                {
                    throw new FormatException(
                        "Timeline CSV argument columns must be contiguous: Arg1, Arg2, Arg3, ...");
                }
            }

            return columns;
        }

        private static string GetCell(IReadOnlyList<string> row, int index)
        {
            return index >= 0 && index < row.Count
                ? row[index] ?? string.Empty
                : string.Empty;
        }

        private sealed class HeaderColumn
        {
            public string Name { get; }
            public int Index { get; }
            public int ArgumentNumber { get; }

            public HeaderColumn(string name, int index, int argumentNumber = 0)
            {
                Name = name;
                Index = index;
                ArgumentNumber = argumentNumber;
            }
        }
    }
}
