using System;
using System.Collections.Generic;
using System.Text;

namespace App.Timeline.V2
{
    /// <summary>
    /// Small RFC-4180-style CSV reader shared by timeline-related CSV files.
    /// </summary>
    public sealed class CsvReader : ICsvReader
    {
        public IReadOnlyList<IReadOnlyList<string>> Read(string csvText)
        {
            if (csvText == null) throw new ArgumentNullException(nameof(csvText));

            if (csvText.Length > 0 && csvText[0] == '\uFEFF')
                csvText = csvText.Substring(1);

            var rows = new List<IReadOnlyList<string>>();
            var currentRow = new List<string>();
            var currentField = new StringBuilder();
            var insideQuotedField = false;

            for (var index = 0; index < csvText.Length; index++)
            {
                var character = csvText[index];

                if (insideQuotedField)
                {
                    if (character == '"')
                    {
                        if (index + 1 < csvText.Length && csvText[index + 1] == '"')
                        {
                            currentField.Append('"');
                            index++;
                        }
                        else
                        {
                            insideQuotedField = false;
                        }
                    }
                    else
                    {
                        currentField.Append(character);
                    }

                    continue;
                }

                if (character == '"')
                {
                    insideQuotedField = true;
                }
                else if (character == ',')
                {
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                }
                else if (character == '\r' || character == '\n')
                {
                    if (character == '\r' && index + 1 < csvText.Length && csvText[index + 1] == '\n')
                        index++;

                    FinishRow(rows, currentRow, currentField);
                    currentRow = new List<string>();
                }
                else
                {
                    currentField.Append(character);
                }
            }

            if (insideQuotedField)
                throw new FormatException("CSV contains an unterminated quoted field.");

            if (currentField.Length > 0 || currentRow.Count > 0)
                FinishRow(rows, currentRow, currentField);

            return rows;
        }

        private static void FinishRow(
            ICollection<IReadOnlyList<string>> rows,
            ICollection<string> currentRow,
            StringBuilder currentField)
        {
            currentRow.Add(currentField.ToString());
            currentField.Clear();

            var values = new List<string>(currentRow);
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    rows.Add(values);
                    return;
                }
            }
        }
    }
}
