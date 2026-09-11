using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public sealed class ImageRepository :
    IImageRepository,
    IDisposable
{
    private readonly ICsvReader _csvReader;
    private readonly IImageLoader _imageLoader;

    private readonly string _csvPath;
    private readonly string _imageRootDirectory;

    private readonly Dictionary<ImageId, LoadedImage>
        _images = new();

    private bool _initialized;

    public ImageRepository(
        ICsvReader csvReader,
        IImageLoader imageLoader,
        string csvPath,
        string imageRootDirectory)
    {
        _csvReader = csvReader ??
            throw new ArgumentNullException(nameof(csvReader));

        _imageLoader = imageLoader ??
            throw new ArgumentNullException(nameof(imageLoader));

        _csvPath = csvPath;
        _imageRootDirectory = imageRootDirectory;
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
                var idText =
                    row.Get("id").Trim();

                var pathText =
                    row.Get("path").Trim();

                ValidateRow(
                    idText,
                    pathText);

                var id =
                    new ImageId(idText);

                if (_images.ContainsKey(id))
                {
                    throw new InvalidDataException(
                        $"Duplicate ImageId: {id}");
                }

                var fullPath =
                    ResolvePath(
                        _imageRootDirectory,
                        pathText);

                var image =
                    await _imageLoader.LoadAsync(
                        fullPath);

                _images.Add(
                    id,
                    image);
            }

            _initialized = true;
        }
        catch
        {
            ReleaseAll();
            throw;
        }
    }

    public Sprite Get(ImageId id)
    {
        EnsureInitialized();

        if (!_images.TryGetValue(
                id,
                out var image))
        {
            throw new KeyNotFoundException(
                $"ImageId is not registered: {id}");
        }

        return image.Sprite;
    }

    public bool TryGet(
        ImageId id,
        out Sprite sprite)
    {
        EnsureInitialized();

        if (_images.TryGetValue(
                id,
                out var image))
        {
            sprite = image.Sprite;
            return true;
        }

        sprite = null;
        return false;
    }

    public void Dispose()
    {
        ReleaseAll();

        _initialized = false;
    }

    private void ReleaseAll()
    {
        foreach (var image in _images.Values)
        {
            image.Dispose();
        }

        _images.Clear();
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException(
                "ImageRepository has not been initialized.");
        }
    }

    private static void ValidateRow(
        string id,
        string path)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidDataException(
                "Image id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidDataException(
                $"Image path must not be empty. id={id}");
        }
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
            Path.Combine(
                rootDirectory,
                path));
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