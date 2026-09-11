public interface ILightScenarioRepository
{
    string GetPath(LightScenarioId id);

    bool TryGetPath(
        LightScenarioId id,
        out string path);
}