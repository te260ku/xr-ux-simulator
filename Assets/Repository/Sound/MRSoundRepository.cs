using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public sealed class MRSoundRepository
    : ISoundRepository<AudioClip>,
      IDisposable
{
    private readonly Dictionary<SoundId, AudioClip> _clips = new();

    private readonly ICsvReader _csvReader;
    private readonly IAudioClipLoader _audioClipLoader;

    private readonly string _csvPath;
    private readonly string _soundRootDirectory;

    private bool _initialized;

    public MRSoundRepository(
        ICsvReader csvReader,
        IAudioClipLoader audioClipLoader,
        string csvPath,
        string soundRootDirectory)
    {
        _csvReader = csvReader;
        _audioClipLoader = audioClipLoader;
        _csvPath = csvPath;
        _soundRootDirectory = soundRootDirectory;
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        var table = _csvReader.Read(_csvPath);

        ValidateColumns(table);

        try
        {
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

                if (_clips.ContainsKey(id))
                {
                    throw new InvalidDataException(
                        $"Duplicate SoundId: {id}");
                }

                var fullPath = ResolvePath(
                    _soundRootDirectory,
                    pathText);

                var clip = await _audioClipLoader.LoadAsync(
                    fullPath);

                clip.name = id.Value;

                _clips.Add(id, clip);
            }

            _initialized = true;
        }
        catch
        {
            ReleaseAll();
            throw;
        }
    }

    public AudioClip Get(SoundId id)
    {
        EnsureInitialized();

        if (!_clips.TryGetValue(id, out var clip))
        {
            throw new KeyNotFoundException(
                $"SoundId is not registered: {id}");
        }

        return clip;
    }

    public bool TryGet(
        SoundId id,
        out AudioClip clip)
    {
        EnsureInitialized();

        return _clips.TryGetValue(id, out clip);
    }

    public void Dispose()
    {
        ReleaseAll();

        _initialized = false;
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException(
                "MRSoundRepository has not been initialized.");
        }
    }

    private void ReleaseAll()
    {
        foreach (var clip in _clips.Values)
        {
            if (clip != null)
            {
                UnityEngine.Object.Destroy(clip);
            }
        }

        _clips.Clear();
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