using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public sealed class TimelineRepository
{
    private readonly TimelineCommandRegistry _commands;
    private readonly JsonSerializer _serializer;

    private Timeline _current =
        new Timeline(Array.Empty<TimelineEvent>());

    public Timeline Current => _current;

    public TimelineRepository(
        TimelineCommandRegistry commands,
        JsonSerializer serializer)
    {
        _commands = commands
            ?? throw new ArgumentNullException(nameof(commands));

        _serializer = serializer
            ?? throw new ArgumentNullException(nameof(serializer));
    }

    public void Load(TimelineFilePath path)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));

        var data = Read(path);
        var events = CreateEvents(data);
        var timeline = new Timeline(events);

        _current = timeline;
    }

    private TimelineEventData[] Read(TimelineFilePath path)
    {
        using var stream = File.OpenText(path.Value);
        using var reader = new JsonTextReader(stream);

        return _serializer.Deserialize<TimelineEventData[]>(reader)
            ?? throw new JsonSerializationException(
                "Timeline could not be deserialized.");
    }

    private TimelineEvent[] CreateEvents(
        IReadOnlyList<TimelineEventData> source)
    {
        var events = new TimelineEvent[source.Count];

        for (var i = 0; i < source.Count; i++)
            events[i] = CreateEvent(source[i]);

        return events;
    }

    private TimelineEvent CreateEvent(TimelineEventData data)
    {
        if (data == null)
            throw new JsonSerializationException(
                "Timeline event must not be null.");

        var time = new TimelineTime(data.Time);
        var execution = CreateExecution(data.Execution);

        return new TimelineEvent(time, execution);
    }

    private TimelineCommandExecution CreateExecution(
        TimelineExecutionData data)
    {
        if (data == null)
            throw new JsonSerializationException(
                "Execution must not be null.");

        var id = new TimelineCommandId(data.Command);
        var command = _commands.Get(id);

        var arguments = DeserializeArguments(
            data.Arguments,
            command.Parameters);

        return new TimelineCommandExecution(
            command,
            arguments);
    }

    private object[] DeserializeArguments(
        JObject source,
        IReadOnlyList<ParameterInfo> parameters)
    {
        if (source == null)
            throw new JsonSerializationException(
                "Arguments must not be null.");

        var values = new object[parameters.Count];

        for (var i = 0; i < parameters.Count; i++)
            values[i] = DeserializeArgument(source, parameters[i]);

        EnsureNoUnknownArguments(source, parameters);
        return values;
    }

    private object DeserializeArgument(
        JObject source,
        ParameterInfo parameter)
    {
        var name = parameter.Name
            ?? throw new InvalidOperationException(
                "Timeline command parameter has no name.");

        if (!source.TryGetValue(
                name,
                StringComparison.Ordinal,
                out var token))
        {
            throw new JsonSerializationException(
                $"Argument '{name}' is missing.");
        }

        if (token.Type == JTokenType.Null)
            throw new JsonSerializationException(
                $"Argument '{name}' must not be null.");

        if (parameter.ParameterType ==
            typeof(TimelineCommandExecution))
        {
            var executionData =
                token.ToObject<TimelineExecutionData>(_serializer);

            return CreateExecution(
                executionData
                ?? throw new JsonSerializationException(
                    "Execution could not be deserialized."));
        }

        return token.ToObject(
                   parameter.ParameterType,
                   _serializer)
               ?? throw new JsonSerializationException(
                   $"Argument '{name}' could not be deserialized.");
    }

    private void EnsureNoUnknownArguments(
        JObject source,
        IReadOnlyList<ParameterInfo> parameters)
    {
        foreach (var property in source.Properties())
        {
            if (!ContainsParameter(
                    parameters,
                    property.Name))
            {
                throw new JsonSerializationException(
                    $"Unknown argument '{property.Name}'.");
            }
        }
    }

    private bool ContainsParameter(
        IReadOnlyList<ParameterInfo> parameters,
        string name)
    {
        foreach (var parameter in parameters)
        {
            if (string.Equals(
                    parameter.Name,
                    name,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
