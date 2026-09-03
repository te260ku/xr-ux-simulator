using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RuntimeCsvEditor
{
    [Serializable]
    public sealed class CsvTableData
    {
        public List<string> Headers { get; } = new();
        public List<List<string>> Rows { get; } = new();

        public int ColumnCount => Headers.Count;

        public void Clear()
        {
            Headers.Clear();
            Rows.Clear();
        }

        public void AddEmptyRow()
        {
            var row = new List<string>(ColumnCount);
            for (int i = 0; i < ColumnCount; i++)
                row.Add(string.Empty);

            Rows.Add(row);
        }

        public void RemoveRowAt(int index)
        {
            if (index < 0 || index >= Rows.Count)
                return;

            Rows.RemoveAt(index);
        }

        public static CsvTableData Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("CSV path is empty.", nameof(path));

            if (!File.Exists(path))
                throw new FileNotFoundException("CSV file was not found.", path);

            string text = File.ReadAllText(path, Encoding.UTF8);
            return Parse(text);
        }

        public void Save(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("CSV path is empty.", nameof(path));

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, ToCsv(), new UTF8Encoding(false));
        }

        public static CsvTableData Parse(string csv)
        {
            var table = new CsvTableData();
            var records = ParseRecords(csv ?? string.Empty);

            if (records.Count == 0)
                return table;

            table.Headers.AddRange(records[0]);

            for (int i = 1; i < records.Count; i++)
            {
                var source = records[i];

                // Ignore a completely empty trailing record.
                if (source.Count == 1 && source[0].Length == 0 && i == records.Count - 1)
                    continue;

                var row = new List<string>(table.ColumnCount);
                for (int c = 0; c < table.ColumnCount; c++)
                    row.Add(c < source.Count ? source[c] : string.Empty);

                table.Rows.Add(row);
            }

            return table;
        }

        public string ToCsv()
        {
            var sb = new StringBuilder();
            WriteRecord(sb, Headers);

            foreach (var row in Rows)
            {
                var normalized = new List<string>(ColumnCount);
                for (int c = 0; c < ColumnCount; c++)
                    normalized.Add(c < row.Count ? row[c] : string.Empty);

                WriteRecord(sb, normalized);
            }

            return sb.ToString();
        }

        private static List<List<string>> ParseRecords(string csv)
        {
            var records = new List<List<string>>();
            var record = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < csv.Length; i++)
            {
                char ch = csv[i];

                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
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
                        inQuotes = true;
                        break;

                    case ',':
                        record.Add(field.ToString());
                        field.Clear();
                        break;

                    case '\r':
                        if (i + 1 < csv.Length && csv[i + 1] == '\n')
                            i++;

                        record.Add(field.ToString());
                        field.Clear();
                        records.Add(record);
                        record = new List<string>();
                        break;

                    case '\n':
                        record.Add(field.ToString());
                        field.Clear();
                        records.Add(record);
                        record = new List<string>();
                        break;

                    default:
                        field.Append(ch);
                        break;
                }
            }

            if (inQuotes)
                throw new FormatException("CSV contains an unterminated quoted field.");

            if (field.Length > 0 || record.Count > 0)
            {
                record.Add(field.ToString());
                records.Add(record);
            }

            return records;
        }

        private static void WriteRecord(StringBuilder sb, IReadOnlyList<string> fields)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');

                sb.Append(Escape(fields[i] ?? string.Empty));
            }

            sb.AppendLine();
        }

        private static string Escape(string value)
        {
            bool needsQuotes = value.Contains(',') ||
                               value.Contains('"') ||
                               value.Contains('\r') ||
                               value.Contains('\n');

            if (!needsQuotes)
                return value;

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }
}
