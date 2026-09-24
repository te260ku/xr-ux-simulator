using System;

namespace TimelineSystem
{
    public sealed class TimelinePlayer
    {
        private readonly TimelineEventRepository _eventRepository;

        private float _elapsedTime;
        private int _nextEventIndex;
        private bool _isPlaying;

        public bool IsPlaying => _isPlaying;

        public TimelinePlayer(
            TimelineEventRepository eventRepository)
        {
            _eventRepository =
                eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
        }

        public void Play()
        {
            _isPlaying = true;
        }

        public void Stop()
        {
            _isPlaying = false;
        }

        public void Reset()
        {
            _elapsedTime = 0f;
            _nextEventIndex = 0;
        }

        public void Update(float deltaTime)
        {
            if (!_isPlaying)
                return;

            _elapsedTime += deltaTime;

            ExecuteEventsUpToCurrentTime();
        }

        private void ExecuteEventsUpToCurrentTime()
        {
            var events = _eventRepository.Events;

            while (_nextEventIndex < events.Count)
            {
                var nextEvent = events[_nextEventIndex];

                if (nextEvent.Time > _elapsedTime)
                    break;

                nextEvent.Execute();
                _nextEventIndex++;
            }
        }
    }
}
