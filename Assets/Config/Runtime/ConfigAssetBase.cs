using System;
using UnityEngine;

public abstract class ConfigAssetBase : ScriptableObject
{
    public abstract string ConfigId { get; }

    public abstract Type DataType { get; }

    public abstract string ToJson();

    public abstract void ApplyJson(string json);
}