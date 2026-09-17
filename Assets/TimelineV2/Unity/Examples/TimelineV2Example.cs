#if TIMELINE_V2_EXAMPLES
using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace App.Timeline.V2.Examples
{
    [TimelineOptionSource("SoundRepository")]
    public sealed class SoundId : IRepositoryKey
    {
        public string Value { get; }

        public SoundId(string value)
        {
            Value = value;
        }
    }

    [TimelineOptionSource("LightRepository")]
    public sealed class LightScenarioId : IRepositoryKey
    {
        public string Value { get; }

        public LightScenarioId(string value)
        {
            Value = value;
        }
    }

    [Flags]
    public enum SoundArea
    {
        None = 0,
        Front = 1 << 0,
        Rear = 1 << 1
    }

    public sealed class SoundOutputSettings
    {
        public float Volume { get; }
        public SoundArea Areas { get; }

        public SoundOutputSettings(float volume, SoundArea areas)
        {
            Volume = volume;
            Areas = areas;
        }
    }

    public sealed class PlaySoundRequest
    {
        public SoundId SoundId { get; }
        public SoundOutputSettings Output { get; }

        public PlaySoundRequest(SoundId soundId, SoundOutputSettings output)
        {
            SoundId = soundId;
            Output = output;
        }
    }

    public sealed class ExampleSoundController
    {
        [TimelineCommand]
        public void PlaySound(PlaySoundRequest request)
        {
            Debug.Log(
                $"PlaySound: id={request.SoundId.Value}, " +
                $"volume={request.Output.Volume}, areas={request.Output.Areas}");
        }
    }

    public sealed class ExampleLightController
    {
        [TimelineCommand]
        public void PlayLight(LightScenarioId scenarioId, bool loop = false)
        {
            Debug.Log($"PlayLight: id={scenarioId.Value}, loop={loop}");
        }
    }

    public sealed class TimelineV2LifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<ExampleSoundController>(Lifetime.Singleton);
            builder.Register<ExampleLightController>(Lifetime.Singleton);
            builder.Register<DiagCommandStore>(Lifetime.Singleton);

            builder.Register<TimelineCommandRegistry>(Lifetime.Singleton);
            builder.Register<ICsvReader, CsvReader>(Lifetime.Singleton);
            builder.Register<TimelineCsvLoader>(Lifetime.Singleton);
            builder.Register<TimelineCommandInvoker>(Lifetime.Singleton);
            builder.Register<DiagCommandExecutor>(Lifetime.Singleton);
            builder.Register<TimelinePlayer>(Lifetime.Singleton);

            // If DiagCommandKeyboardInput is placed in the scene, register it too:
            // builder.RegisterComponentInHierarchy<DiagCommandKeyboardInput>();
        }
    }
}
#endif
