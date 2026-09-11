using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public sealed class AudioClipLoader : IAudioClipLoader
{
    public async Task<AudioClip> LoadAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(
                "Audio file path must not be empty.",
                nameof(filePath));
        }

        var fullPath = Path.GetFullPath(filePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Audio file was not found: {fullPath}",
                fullPath);
        }

        var audioType = GetAudioType(fullPath);
        var uri = new Uri(fullPath).AbsoluteUri;

        using var request =
            UnityWebRequestMultimedia.GetAudioClip(
                uri,
                audioType);

        var operation = request.SendWebRequest();

        while (!operation.isDone)
        {
            await Task.Yield();
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            throw new IOException(
                $"Failed to load audio file.\n" +
                $"Path: {fullPath}\n" +
                $"Error: {request.error}");
        }

        var clip =
            DownloadHandlerAudioClip.GetContent(request);

        if (clip == null)
        {
            throw new IOException(
                $"Failed to create AudioClip: {fullPath}");
        }

        return clip;
    }

    private static AudioType GetAudioType(string filePath)
    {
        var extension =
            Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".wav" => AudioType.WAV,
            ".mp3" => AudioType.MPEG,
            ".ogg" => AudioType.OGGVORBIS,
            ".aif" => AudioType.AIFF,
            ".aiff" => AudioType.AIFF,

            _ => throw new NotSupportedException(
                $"Unsupported audio format: {extension}")
        };
    }
}