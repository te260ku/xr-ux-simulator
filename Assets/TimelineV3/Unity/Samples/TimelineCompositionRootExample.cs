using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class TimelineCompositionRootExample :
    LifetimeScope
{
    [SerializeField]
    private string _timelineRelativePath =
        "Config/timeline.json";

    protected override void Configure(
        IContainerBuilder builder)
    {
        var settings = new JsonSerializerSettings
        {
            ContractResolver =
                new CamelCasePropertyNamesContractResolver()
        };

        settings.Converters.Add(
            new StringEnumConverter());

        builder.RegisterInstance(
            JsonSerializer.Create(settings));

        builder.RegisterInstance(
            new TimelineFilePath(
                Path.Combine(
                    Application.streamingAssetsPath,
                    _timelineRelativePath)));

        builder.Register<TimelineCommandScanner>(
            Lifetime.Singleton);

        builder.Register<DiagCommandRegistry>(
            Lifetime.Singleton);

        builder.Register<DiagCommandAssigner>(
            Lifetime.Singleton);

        builder.Register<DiagCommandExecutor>(
            Lifetime.Singleton);

        builder.Register<ExampleTimelineCommands>(
            Lifetime.Singleton);

        builder.Register<TimelineCommandRegistry>(
            Lifetime.Singleton);

        builder.Register<TimelineRepository>(
            Lifetime.Singleton);

        builder.Register<TimelinePlayer>(
            Lifetime.Singleton);

        builder.RegisterEntryPoint<TimelineInitializer>();
        builder.RegisterEntryPoint<TimelineUpdater>();
    }
}
