using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace TimelineSystem
{
    public sealed class TimelineEventRepository
    {
        private readonly TimelineCommandRegistry _commandRegistry;
        private readonly TimelineArgumentConverter _argumentConverter;

        private IReadOnlyList<TimelineEvent> _events =
            Array.Empty<TimelineEvent>();

        public IReadOnlyList<TimelineEvent> Events => _events;

        public TimelineEventRepository(
            TimelineCommandRegistry commandRegistry,
            TimelineArgumentConverter argumentConverter)
        {
            _commandRegistry =
                commandRegistry ?? throw new ArgumentNullException(nameof(commandRegistry));

            _argumentConverter =
                argumentConverter ?? throw new ArgumentNullException(nameof(argumentConverter));
        }

        public void Load(string filePath)
        {
            var json = File.ReadAllText(filePath);

            var eventDataList =
                JsonConvert.DeserializeObject<List<TimelineEventData>>(json)
                ?? throw new TimelineLoadException(
                    "Failed to deserialize timeline.");

            var loadedEvents =
                new List<TimelineEvent>(eventDataList.Count);

            for (var i = 0; i < eventDataList.Count; i++)
            {
                var eventData = eventDataList[i];

                eventData.Validate();

                if (i > 0 &&
                    eventData.Time < eventDataList[i - 1].Time)
                {
                    throw new TimelineLoadException(
                        $"Timeline events must be in chronological order. " +
                        $"Index: {i}, Time: {eventData.Time}.");
                }

                var command =
                    _commandRegistry.Get(eventData.Command);

                var arguments =
                    _argumentConverter.ConvertToMethodArguments(
                        eventData.Arguments,
                        command.Parameters);

                loadedEvents.Add(
                    new TimelineEvent(
                        eventData.Time,
                        command,
                        arguments));
            }

            _events = loadedEvents;
        }
    }
}
