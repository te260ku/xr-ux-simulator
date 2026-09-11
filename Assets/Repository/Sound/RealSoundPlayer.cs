public sealed class RealSoundPlayer : ISoundPlayer
{
    private readonly ISoundRepository<string> _repository;
    private readonly IExternalSoundPlayer _externalPlayer;

    public RealSoundPlayer(
        ISoundRepository<string> repository,
        IExternalSoundPlayer externalPlayer)
    {
        _repository = repository;
        _externalPlayer = externalPlayer;
    }

    public void Play(SoundId id)
    {
        var filePath = _repository.Get(id);

        _externalPlayer.Play(filePath);
    }

    public void Stop()
    {
        _externalPlayer.Stop();
    }
}