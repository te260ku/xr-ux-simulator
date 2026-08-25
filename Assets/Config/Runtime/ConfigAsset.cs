using System;
using UnityEngine;

public abstract class ConfigAsset<TData> : ConfigAssetBase
    where TData : class, new()
{
    [SerializeField]
    private TData data = new();

    public TData Data => data;

    public override Type DataType => typeof(TData);

    public override string ToJson()
    {
        return JsonUtility.ToJson(data, true);
    }

    public override void ApplyJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException(
                $"JSON is empty. ConfigId: {ConfigId}");
        }

        var loadedData = JsonUtility.FromJson<TData>(json);

        if (loadedData == null)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize config. ConfigId: {ConfigId}");
        }

        data = loadedData;
    }
}