using System;
using VContainer.Unity;

public sealed class TimelineInitializer : IStartable
{
    private readonly TimelineRepository _repository;
    private readonly TimelineFilePath _path;

    public TimelineInitializer(
        TimelineRepository repository,
        TimelineFilePath path)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));

        _path = path
            ?? throw new ArgumentNullException(nameof(path));
    }

    public void Start()
    {
        _repository.Load(_path);
    }
}
