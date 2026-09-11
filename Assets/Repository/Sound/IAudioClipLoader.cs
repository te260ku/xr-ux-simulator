using System.Threading.Tasks;
using UnityEngine;

public interface IAudioClipLoader
{
    Task<AudioClip> LoadAsync(string filePath);
}