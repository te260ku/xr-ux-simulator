using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class LedEffectClip
{
    [SerializeField]
    private float _startTime;

    [SerializeField]
    [Min(0.01f)]
    private float _duration = 1f;

    [SerializeField]
    private LedEffectType _effectType = LedEffectType.Static;

    [SerializeField]
    private List<string> _targetLedIds = new();

    [SerializeField]
    private Color _color = Color.white;

    [SerializeField]
    [Min(0f)]
    private float _intensity = 1f;
    [SerializeField]
    private LedDistributionType _distributionType =
        LedDistributionType.Simultaneous;

    [SerializeField]
    private LedDistributionAxis _distributionAxis =
        LedDistributionAxis.X;

    [SerializeField]
    [Min(0f)]
    private float _staggerDuration = 0f;
    public LedDistributionType DistributionType =>
        _distributionType;

    public LedDistributionAxis DistributionAxis =>
        _distributionAxis;

    public float StaggerDuration =>
        Mathf.Max(0f, _staggerDuration);


    public float StartTime => _startTime;

    public float Duration => _duration;

    public float EndTime =>
    _startTime
    + _duration
    + StaggerDuration;

    public LedEffectType EffectType =>
        _effectType;

    public IReadOnlyList<string> TargetLedIds =>
        _targetLedIds;

    public Color Color =>
        _color;

    public float Intensity =>
        _intensity;
}