using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public static class CsvIdReader
{
    public static IReadOnlyList<string> ReadIds(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException($"Repository CSV not found: {path}");
        var rows = Parse(File.ReadAllText(path, Encoding.UTF8));
        if (rows.Count == 0) throw new FormatException("Repository CSV is empty.");
        var column = rows[0].FindIndex(x => x.Trim().TrimStart('\uFEFF').Trim() == "id");
        if (column < 0) throw new FormatException("Repository CSV needs an 'id' column.");
        return CollectIds(rows, column);
    }

    private static IReadOnlyList<string> CollectIds(List<List<string>> rows, int column)
    {
        var result = new List<string>();
        for (var row = 1; row < rows.Count; row++)
        {
            if (rows[row].Count == 1 && rows[row][0] == "") continue;
            if (column >= rows[row].Count || rows[row][column] == "")
                throw new FormatException($"CSV row {row + 1} has no ID.");
            result.Add(rows[row][column]);
        }
        return result;
    }

    private static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        var cells = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        var closedQuote = false;
        var started = false;
        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            if (quoted)
            {
                if (character != '"') { cell.Append(character); continue; }
                if (i + 1 < text.Length && text[i + 1] == '"')
                { cell.Append('"'); i++; continue; }
                quoted = false; closedQuote = true;
                continue;
            }
            if (character == '"' && !started && !closedQuote)
            { quoted = true; started = true; continue; }
            if (character == '"' || (closedQuote && character != ',' &&
                character != '\r' && character != '\n'))
                throw new FormatException($"Invalid CSV near character {i + 1}.");
            if (character == ',' || character == '\r' || character == '\n')
            {
                cells.Add(cell.ToString()); cell.Clear();
                started = false; closedQuote = false;
                if (character == ',') continue;
                if (character == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                rows.Add(cells); cells = new List<string>();
                continue;
            }
            cell.Append(character); started = true;
        }
        if (quoted) throw new FormatException("Unclosed quoted field in CSV.");
        if (started || closedQuote || cells.Count > 0)
        { cells.Add(cell.ToString()); rows.Add(cells); }
        return rows;
    }
}
