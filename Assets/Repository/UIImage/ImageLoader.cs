using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public sealed class ImageLoader : IImageLoader
{
    private readonly float _pixelsPerUnit;

    public ImageLoader(float pixelsPerUnit = 100f)
    {
        if (pixelsPerUnit <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pixelsPerUnit));
        }

        _pixelsPerUnit = pixelsPerUnit;
    }

    public async Task<LoadedImage> LoadAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(
                "Image file path must not be empty.",
                nameof(filePath));
        }

        var fullPath = Path.GetFullPath(filePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Image file was not found: {fullPath}",
                fullPath);
        }

        var uri = new Uri(fullPath).AbsoluteUri;

        using var request =
            UnityWebRequestTexture.GetTexture(
                uri,
                nonReadable: true);

        var operation = request.SendWebRequest();

        while (!operation.isDone)
        {
            await Task.Yield();
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            throw new IOException(
                $"Failed to load image.\n" +
                $"Path: {fullPath}\n" +
                $"Error: {request.error}");
        }

        var texture =
            DownloadHandlerTexture.GetContent(request);

        if (texture == null)
        {
            throw new IOException(
                $"Failed to create Texture2D: {fullPath}");
        }

        texture.name =
            Path.GetFileNameWithoutExtension(fullPath);

        Sprite sprite = null;

        try
        {
            sprite = Sprite.Create(
                texture,
                new Rect(
                    0f,
                    0f,
                    texture.width,
                    texture.height),
                new Vector2(0.5f, 0.5f),
                _pixelsPerUnit);

            if (sprite == null)
            {
                throw new IOException(
                    $"Failed to create Sprite: {fullPath}");
            }

            sprite.name = texture.name;

            return new LoadedImage(
                texture,
                sprite);
        }
        catch
        {
            if (sprite != null)
            {
                UnityEngine.Object.Destroy(sprite);
            }

            UnityEngine.Object.Destroy(texture);

            throw;
        }
    }
}