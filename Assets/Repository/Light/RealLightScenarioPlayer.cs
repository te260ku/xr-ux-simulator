public sealed class RealLightScenarioPlayer
    : ILightScenarioPlayer
{
    private readonly ILightScenarioRepository _repository;
    private readonly IExternalLightScenarioPlayer _externalPlayer;

    public RealLightScenarioPlayer(
        ILightScenarioRepository repository,
        IExternalLightScenarioPlayer externalPlayer)
    {
        _repository = repository;
        _externalPlayer = externalPlayer;
    }

    public void Play(LightScenarioId id)
    {
        var path = _repository.GetPath(id);

        _externalPlayer.Play(path);
    }

    public void Stop()
    {
        _externalPlayer.Stop();
    }
}