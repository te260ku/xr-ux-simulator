// #if TIMELINE_V1_EXAMPLES
using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace App.Timeline.Examples
{
    [TimelineOptionSource("SoundRepository")]
    public readonly struct SoundId : IRepositoryKey
    {
        public string Value { get; }

        public SoundId(string value)
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

    public readonly struct PlaySoundRequest
    {
        public SoundId SoundId { get; }
        public float Volume { get; }
        public SoundArea Area { get; }

        public PlaySoundRequest(
            SoundId soundId,
            float volume,
            SoundArea area)
        {
            SoundId = soundId;
            Volume = volume;
            Area = area;
        }
    }

    public sealed class ExampleSoundController
    {
        [TimelineCommand]
        public void PlaySound(PlaySoundRequest request)
        {
            Debug.Log($"PlaySound: {request.SoundId.Value}, {request.Volume}, {request.Area}");
        }
    }

    public sealed class ExampleDiagController
    {
        [TimelineCommand]
        public void SetDiagCommand(
            int diagCommandId,
            TimelineCommandInvocation assignedCommand)
        {
            Debug.Log($"SetDiagCommand: {diagCommandId} -> {assignedCommand.CommandId}");
        }
    }

    /// <summary>
    /// Example VContainer registration. Command target types must be resolvable as concrete types.
    /// </summary>
    public sealed class TimelineExampleLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<ExampleSoundController>(Lifetime.Singleton);
            builder.Register<ExampleDiagController>(Lifetime.Singleton);

            builder.Register<TimelineCommandScanner>(Lifetime.Singleton);
            builder.Register<TimelineCommandSchemaBuilder>(Lifetime.Singleton);
            builder.Register<TimelineCommandRegistry>(Lifetime.Singleton);
            builder.Register<TimelineArgumentConverter>(Lifetime.Singleton);
            builder.Register<TimelineObjectFactory>(Lifetime.Singleton);
            builder.Register<TimelineCommandArgumentBinder>(Lifetime.Singleton);
            builder.Register<ICsvReader, CsvReader>(Lifetime.Singleton);
            builder.Register<TimelineScenarioLoader>(Lifetime.Singleton);
            builder.Register<TimelineCommandInvoker>(Lifetime.Singleton);
            builder.Register<TimelinePlayer>(Lifetime.Singleton);
        }
    }
}

// #endif
