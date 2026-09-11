using System;
using System.Collections.Generic;
using System.IO;

public sealed class LightScenarioRepository
    : ILightScenarioRepository
{
    private readonly Dictionary<LightScenarioId, string> _paths;

    public LightScenarioRepository(
        ICsvReader csvReader,
        string csvPath,
        string scenarioRootDirectory)
    {
        if (csvReader == null)
        {
            throw new ArgumentNullException(nameof(csvReader));
        }

        if (string.IsNullOrWhiteSpace(csvPath))
        {
            throw new ArgumentException(
                "CSV path must not be empty.",
                nameof(csvPath));
        }

        if (string.IsNullOrWhiteSpace(scenarioRootDirectory))
        {
            throw new ArgumentException(
                "Scenario root directory must not be empty.",
                nameof(scenarioRootDirectory));
        }

        var table = csvReader.Read(csvPath);

        ValidateColumns(table);

        _paths = new Dictionary<LightScenarioId, string>();

        foreach (var row in table.Rows)
        {
            var idText = row.Get("id").Trim();
            var pathText = row.Get("path").Trim();

            if (string.IsNullOrWhiteSpace(idText))
            {
                throw new InvalidDataException(
                    "Light scenario id must not be empty.");
            }

            if (string.IsNullOrWhiteSpace(pathText))
            {
                throw new InvalidDataException(
                    $"Light scenario path must not be empty. id={idText}");
            }

            var id = new LightScenarioId(idText);

            var fullPath = ResolvePath(
                scenarioRootDirectory,
                pathText);

            if (!_paths.TryAdd(id, fullPath))
            {
                throw new InvalidDataException(
                    $"Duplicate LightScenarioId: {id}");
            }
        }
    }

    public string GetPath(LightScenarioId id)
    {
        if (!_paths.TryGetValue(id, out var path))
        {
            throw new KeyNotFoundException(
                $"LightScenarioId is not registered: {id}");
        }

        return path;
    }

    public bool TryGetPath(
        LightScenarioId id,
        out string path)
    {
        return _paths.TryGetValue(id, out path);
    }

    private static string ResolvePath(
        string rootDirectory,
        string path)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        return Path.GetFullPath(
            Path.Combine(rootDirectory, path));
    }

    private static void ValidateColumns(
        CsvTable table)
    {
        if (!ContainsColumn(table, "id"))
        {
            throw new InvalidDataException(
                "Required CSV column is missing: id");
        }

        if (!ContainsColumn(table, "path"))
        {
            throw new InvalidDataException(
                "Required CSV column is missing: path");
        }
    }

    private static bool ContainsColumn(
        CsvTable table,
        string column)
    {
        foreach (var header in table.Headers)
        {
            if (string.Equals(
                    header,
                    column,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}