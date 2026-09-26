using UnityEngine;

public enum UiState
{
    Normal,
    Disabled,
    Hidden
}

public sealed class ExampleTimelineCommands
{
    [TimelineCommand("PlaySound")]
    public void PlaySound(
        [TimelineParameterDescription("再生する音声ID。")]
        SoundId soundId,
        [TimelineParameterDescription("元のクラス")]
        Req req,
        [TimelineParameterDescription("再生音量。0.0〜1.0。")]
        [TimelineParameterRange(0.0, 1.0)]
        float volume)
    {
        Debug.Log(
            $"PlaySound: {soundId.Value}, volume={volume}");
    }

    [TimelineCommand("SetUiState")]
    public void SetUiState(
        [TimelineParameterDescription("状態を変更するUIのID。")]
        UiTargetId target,
        [TimelineParameterDescription("UIへ設定する状態。")]
        UiState state)
    {
        Debug.Log(
            $"SetUiState: {target.Value}, state={state}");
    }
}


public sealed class Req
{
    [TimelineParameterDescription("a1の説明")]
    [TimelineParameterRange(0.0, 1.0)]
    public float a1;

    [TimelineParameterDescription("a2の説明")]
    [TimelineParameterRange(0, 100)]
    public int a2;

    [TimelineParameterDescription("a3の説明")]
    public UiState a3;

    [TimelineParameterDescription("a4の説明")]
    public bool a4;
}
