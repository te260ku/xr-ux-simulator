using System;

public sealed class TimelinePlayer
{
    private readonly TimelineRepository _repository;

    private float _elapsedTime;
    private int _nextEventIndex;
    private bool _isPlaying;

    public bool IsPlaying => _isPlaying;

    public TimelinePlayer(TimelineRepository repository)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
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
        EnsureDeltaTimeIsValid(deltaTime);

        if (!_isPlaying)
            return;

        _elapsedTime += deltaTime;
        ExecuteDueEvents();
    }

    private void ExecuteDueEvents()
    {
        var timeline = _repository.Current;

        while (_nextEventIndex < timeline.Count)
        {
            var timelineEvent =
                timeline.Get(_nextEventIndex);

            if (timelineEvent.Time.Seconds > _elapsedTime)
                return;

            timelineEvent.Execute();
            _nextEventIndex++;
        }
    }

    private void EnsureDeltaTimeIsValid(float deltaTime)
    {
        if (float.IsNaN(deltaTime) ||
            float.IsInfinity(deltaTime) ||
            deltaTime < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deltaTime));
        }
    }
}
