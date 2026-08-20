namespace LightingScenarioTool
{
    public interface IScenarioExporter
    {
        void Export(string path, ScenarioData scenario);
    }
}
