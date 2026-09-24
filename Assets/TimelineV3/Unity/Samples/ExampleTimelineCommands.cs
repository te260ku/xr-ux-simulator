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
        SoundId soundId,
        float volume)
    {
        Debug.Log(
            $"PlaySound: {soundId.Value}, volume={volume}");
    }

    [TimelineCommand("SetUiState")]
    public void SetUiState(
        UiTargetId target,
        UiState state)
    {
        Debug.Log(
            $"SetUiState: {target.Value}, state={state}");
    }
}
