using System;
using UnityEngine;

[Serializable]
public sealed class LightingConfigData
{
    public float brightness = 1.0f;

    public float fadeDuration = 0.5f;

    public Color defaultColor = Color.white;
}