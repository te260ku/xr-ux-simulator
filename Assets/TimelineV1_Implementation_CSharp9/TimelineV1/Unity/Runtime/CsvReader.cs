using System;
using System.Collections.Generic;

namespace App.Timeline
{
    public sealed class CsvReader : ICsvReader
    {
        public IReadOnlyList<IReadOnlyList<string>> Read(string csvText)
        {
            if (csvText == null) throw new ArgumentNullException(nameof(csvText));

            if (csvText.Length > 0 && csvText[0] == '\uFEFF')
                csvText = csvText.Substring(1);

            var result = new List<IReadOnlyList<string>>();
            var row = new List<string>();
            var field = new System.Text.StringBuilder();
            var quoted = false;

            for (var i = 0; i < csvText.Length; i++)
            {
                var ch = csvText[i];

                if (quoted)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < csvText.Length && csvText[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            quoted = false;
                        }
                    }
                    else
                    {
                        field.Append(ch);
                    }

                    continue;
                }

                switch (ch)
                {
                    case '"':
                        quoted = true;
                        break;
                    case ',':
                        row.Add(field.ToString());
                        field.Clear();
                        break;
                    case '\r':
                        if (i + 1 < csvText.Length && csvText[i + 1] == '\n') i++;
                        FinishRow(result, row, field);
                        row = new List<string>();
                        break;
                    case '\n':
                        FinishRow(result, row, field);
                        row = new List<string>();
                        break;
                    default:
                        field.Append(ch);
                        break;
                }
            }

            if (quoted)
                throw new FormatException("CSV contains an unterminated quoted field.");

            if (field.Length > 0 || row.Count > 0)
                FinishRow(result, row, field);

            return result;
        }

        private static void FinishRow(
            ICollection<IReadOnlyList<string>> result,
            ICollection<string> row,
            System.Text.StringBuilder field)
        {
            row.Add(field.ToString());
            field.Clear();

            var values = new List<string>(row);
            var hasValue = false;
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    hasValue = true;
                    break;
                }
            }

            if (hasValue) result.Add(values);
        }
    }
}
