using UnityEngine;

public readonly struct LedTargetInfo
{
    public string Id { get; }

    public Vector3 Position { get; }


    public LedTargetInfo(
        string id,
        Vector3 position)
    {
        Id = id;
        Position = position;
    }
}