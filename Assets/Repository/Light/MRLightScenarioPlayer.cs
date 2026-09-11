public sealed class MRLightScenarioPlayer
    : ILightScenarioPlayer
{
    private readonly ILightScenarioRepository _repository;
    private readonly IMRLightScenarioRuntime _runtime;

    public MRLightScenarioPlayer(
        ILightScenarioRepository repository,
        IMRLightScenarioRuntime runtime)
    {
        _repository = repository;
        _runtime = runtime;
    }

    public void Play(LightScenarioId id)
    {
        var path = _repository.GetPath(id);

        _runtime.Play(path);
    }

    public void Stop()
    {
        _runtime.Stop();
    }
}