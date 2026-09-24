using UnityEngine;

namespace TimelineSystem.Examples
{
    public enum SpeakerPosition
    {
        Front,
        Rear
    }

    public sealed class SoundSettings
    {
        public float Volume { get; set; }
        public SpeakerPosition Position { get; set; }
    }

    public sealed class PlaySoundRequest
    {
        public SoundId SoundId { get; set; }
        public SoundSettings Settings { get; set; }
    }

    public sealed class ExampleSoundController
    {
        [TimelineCommand("PlaySound")]
        public void PlaySound(PlaySoundRequest request)
        {
            Debug.Log(
                $"PlaySound: Id={request.SoundId}, " +
                $"Volume={request.Settings.Volume}, " +
                $"Position={request.Settings.Position}");
        }
    }
}
