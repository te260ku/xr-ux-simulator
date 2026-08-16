using UnityEngine;

public readonly struct LedState
{
    public Color Color { get; }
    public float Intensity { get; }

    public bool IsOn => Intensity > 0f;

    public LedState(Color color, float intensity)
    {
        Color = color;
        Intensity = Mathf.Max(0f, intensity);
    }

    public static LedState Off =>
        new(Color.white, 0f);
}