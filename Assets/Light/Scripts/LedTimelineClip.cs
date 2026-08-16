using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public sealed class LedTimelineClip
    : PlayableAsset,
      ITimelineClipAsset
{
    [Header("LED")]

    public Gradient Color = new Gradient();

    public AnimationCurve Intensity =
        AnimationCurve.Linear(
            0f, 1f,
            1f, 1f);


    public ClipCaps clipCaps =>
        ClipCaps.Blending;


    public override Playable CreatePlayable(
        PlayableGraph graph,
        GameObject owner)
    {
        var playable =
            ScriptPlayable<LedTimelineBehaviour>
                .Create(graph);

        var behaviour =
            playable.GetBehaviour();

        behaviour.Color = Color;
        behaviour.Intensity = Intensity;

        return playable;
    }
}