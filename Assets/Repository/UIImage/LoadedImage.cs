using System;
using UnityEngine;

public sealed class LoadedImage : IDisposable
{
    public Texture2D Texture { get; }
    public Sprite Sprite { get; }

    private bool _disposed;

    public LoadedImage(
        Texture2D texture,
        Sprite sprite)
    {
        Texture = texture ??
            throw new ArgumentNullException(nameof(texture));

        Sprite = sprite ??
            throw new ArgumentNullException(nameof(sprite));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        if (Sprite != null)
        {
            UnityEngine.Object.Destroy(Sprite);
        }

        if (Texture != null)
        {
            UnityEngine.Object.Destroy(Texture);
        }

        _disposed = true;
    }
}