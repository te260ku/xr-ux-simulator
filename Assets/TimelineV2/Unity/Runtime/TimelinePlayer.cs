using System;

namespace App.Timeline.V2
{
    /// <summary>
    /// Advances timeline time and invokes every event whose scheduled time has been reached.
    /// </summary>
    public sealed class TimelinePlayer
    {
        private readonly TimelineCommandInvoker commandInvoker;
        private TimelineScenario scenario;
        private int nextEventIndex;

        public double CurrentTimeSeconds { get; private set; }
        public bool IsPlaying { get; private set; }
        public TimelineScenario Scenario => scenario;

        public TimelinePlayer(TimelineCommandInvoker commandInvoker)
        {
            this.commandInvoker = commandInvoker ?? throw new ArgumentNullException(nameof(commandInvoker));
        }

        public void Load(TimelineScenario timelineScenario)
        {
            scenario = timelineScenario ?? throw new ArgumentNullException(nameof(timelineScenario));
            CurrentTimeSeconds = 0;
            nextEventIndex = 0;
            IsPlaying = false;
        }

        public void Play()
        {
            EnsureScenarioLoaded();
            IsPlaying = true;
            InvokeReachedEvents();
        }

        public void Pause()
        {
            IsPlaying = false;
        }

        public void Stop()
        {
            IsPlaying = false;
            CurrentTimeSeconds = 0;
            nextEventIndex = 0;
        }

        public void Tick(double deltaTimeSeconds)
        {
            if (!IsPlaying) return;
            if (deltaTimeSeconds < 0 || double.IsNaN(deltaTimeSeconds) || double.IsInfinity(deltaTimeSeconds))
                throw new ArgumentOutOfRangeException(nameof(deltaTimeSeconds));

            CurrentTimeSeconds += deltaTimeSeconds;
            InvokeReachedEvents();
        }

        public void Seek(double timeSeconds, bool invokeSkippedEvents = false)
        {
            EnsureScenarioLoaded();
            if (timeSeconds < 0 || double.IsNaN(timeSeconds) || double.IsInfinity(timeSeconds))
                throw new ArgumentOutOfRangeException(nameof(timeSeconds));

            if (invokeSkippedEvents && timeSeconds >= CurrentTimeSeconds)
            {
                CurrentTimeSeconds = timeSeconds;
                InvokeReachedEvents();
                return;
            }

            CurrentTimeSeconds = timeSeconds;
            nextEventIndex = FindNextEventIndex(timeSeconds);
        }

        private void InvokeReachedEvents()
        {
            while (nextEventIndex < scenario.Events.Count &&
                   scenario.Events[nextEventIndex].TimeSeconds <= CurrentTimeSeconds)
            {
                commandInvoker.Invoke(scenario.Events[nextEventIndex]);
                nextEventIndex++;
            }

            if (nextEventIndex >= scenario.Events.Count)
                IsPlaying = false;
        }

        private int FindNextEventIndex(double timeSeconds)
        {
            var low = 0;
            var high = scenario.Events.Count;

            while (low < high)
            {
                var middle = low + ((high - low) / 2);
                if (scenario.Events[middle].TimeSeconds <= timeSeconds)
                    low = middle + 1;
                else
                    high = middle;
            }

            return low;
        }

        private void EnsureScenarioLoaded()
        {
            if (scenario == null)
                throw new InvalidOperationException("Load a TimelineScenario before playback.");
        }
    }
}
