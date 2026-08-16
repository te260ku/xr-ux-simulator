using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewLedSequence",
    menuName = "LED/Sequence")]
public sealed class LedSequence : ScriptableObject
{
    [SerializeField]
    [Min(0.1f)]
    private float _duration = 5f;

    [SerializeField]
    private bool _loop = true;

    [SerializeField]
    private List<LedEffectClip> _clips = new();


    public float Duration =>
        _duration;

    public bool Loop =>
        _loop;

    public IReadOnlyList<LedEffectClip> Clips =>
        _clips;


    private void OnValidate()
    {
        _duration = Mathf.Max(0.1f, _duration);
    }
}