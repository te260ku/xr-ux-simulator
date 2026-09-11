using System;
using System.Collections.Generic;
using System.IO;

public sealed class RealSoundRepository
    : ISoundRepository<string>
{
    private readonly Dictionary<SoundId, string> _paths;

    public RealSoundRepository(
        ICsvReader csvReader,
        string csvPath,
        string soundRootDirectory)
    {
        var table = csvReader.Read(csvPath);

        ValidateColumns(table);

        _paths = new Dictionary<SoundId, string>();

        foreach (var row in table.Rows)
        {
            var idText = row.Get("id").Trim();
            var pathText = row.Get("path").Trim();

            if (string.IsNullOrWhiteSpace(idText))
            {
                throw new InvalidDataException(
                    "Sound id must not be empty.");
            }

            if (string.IsNullOrWhiteSpace(pathText))
            {
                throw new InvalidDataException(
                    $"Sound path must not be empty. id={idText}");
            }

            var id = new SoundId(idText);

            var fullPath = ResolvePath(
                soundRootDirectory,
                pathText);

            if (!_paths.TryAdd(id, fullPath))
            {
                throw new InvalidDataException(
                    $"Duplicate SoundId: {id}");
            }
        }
    }

    public string Get(SoundId id)
    {
        if (!_paths.TryGetValue(id, out var path))
        {
            throw new KeyNotFoundException(
                $"SoundId is not registered: {id}");
        }

        return path;
    }

    public bool TryGet(
        SoundId id,
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

    private static void ValidateColumns(CsvTable table)
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