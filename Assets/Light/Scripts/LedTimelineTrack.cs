using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(
    1.0f,
    0.45f,
    0.1f)]

[TrackClipType(
    typeof(LedTimelineClip))]

[TrackBindingType(
    typeof(LEDOverlayController))]

public sealed class LedTimelineTrack
    : TrackAsset
{
    public override Playable CreateTrackMixer(
        PlayableGraph graph,
        GameObject go,
        int inputCount)
    {
        return ScriptPlayable<LedTimelineMixer>
            .Create(
                graph,
                inputCount);
    }
}