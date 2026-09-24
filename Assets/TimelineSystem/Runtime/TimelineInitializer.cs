using System;
using VContainer.Unity;

namespace TimelineSystem
{
    public sealed class TimelineInitializer : IStartable
    {
        private readonly TimelineEventRepository _eventRepository;
        private readonly TimelineSettings _settings;

        public TimelineInitializer(
            TimelineEventRepository eventRepository,
            TimelineSettings settings)
        {
            _eventRepository =
                eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
            _settings =
                settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Start()
        {
            _eventRepository.Load(_settings.FilePath);
        }
    }
}
