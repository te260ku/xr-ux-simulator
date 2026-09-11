using System.Collections.Generic;

public sealed class CsvTable
{
    public IReadOnlyList<string> Headers { get; }
    public IReadOnlyList<CsvRow> Rows { get; }

    public CsvTable(
        IReadOnlyList<string> headers,
        IReadOnlyList<CsvRow> rows)
    {
        Headers = headers;
        Rows = rows;
    }
}

public sealed class CsvRow
{
    private readonly IReadOnlyDictionary<string, string> _values;

    public CsvRow(
        IReadOnlyDictionary<string, string> values)
    {
        _values = values;
    }

    public string Get(string columnName)
    {
        if (!_values.TryGetValue(columnName, out var value))
        {
            throw new KeyNotFoundException(
                $"CSV column was not found: {columnName}");
        }

        return value;
    }
}