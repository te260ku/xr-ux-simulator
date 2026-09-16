using System;

namespace App.Timeline
{
    public sealed class TimelinePlayer
    {
        private readonly TimelineCommandInvoker invoker;
        private TimelineScenario scenario;
        private int nextEventIndex;

        public double CurrentTime { get; private set; }
        public bool IsPlaying { get; private set; }
        public TimelineScenario Scenario => scenario;

        public TimelinePlayer(TimelineCommandInvoker invoker)
        {
            this.invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
        }

        public void Load(TimelineScenario value)
        {
            scenario = value ?? throw new ArgumentNullException(nameof(value));
            CurrentTime = 0;
            nextEventIndex = 0;
            IsPlaying = false;
        }

        public void Play()
        {
            if (scenario == null) throw new InvalidOperationException("Load a timeline scenario before playback.");
            IsPlaying = true;
        }

        public void Pause() => IsPlaying = false;

        public void Stop()
        {
            IsPlaying = false;
            CurrentTime = 0;
            nextEventIndex = 0;
        }

        public void Tick(double deltaTime)
        {
            if (!IsPlaying) return;
            if (deltaTime < 0 || double.IsNaN(deltaTime) || double.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(deltaTime));

            CurrentTime += deltaTime;
            DispatchReachedEvents();
        }

        public void Seek(double time, bool executeSkippedEvents = false)
        {
            if (scenario == null) throw new InvalidOperationException("Load a timeline scenario before seeking.");
            if (time < 0 || double.IsNaN(time) || double.IsInfinity(time))
                throw new ArgumentOutOfRangeException(nameof(time));

            if (executeSkippedEvents && time >= CurrentTime)
            {
                CurrentTime = time;
                DispatchReachedEvents();
                return;
            }

            CurrentTime = time;
            nextEventIndex = FindNextEventIndex(time);
        }

        private void DispatchReachedEvents()
        {
            if (scenario == null) return;

            while (nextEventIndex < scenario.Events.Count &&
                   scenario.Events[nextEventIndex].Time <= CurrentTime)
            {
                invoker.Invoke(scenario.Events[nextEventIndex].Command);
                nextEventIndex++;
            }

            if (nextEventIndex >= scenario.Events.Count)
                IsPlaying = false;
        }

        private int FindNextEventIndex(double time)
        {
            var low = 0;
            var high = scenario.Events.Count;

            while (low < high)
            {
                var mid = low + ((high - low) / 2);
                if (scenario.Events[mid].Time <= time)
                    low = mid + 1;
                else
                    high = mid;
            }

            return low;
        }
    }
}
