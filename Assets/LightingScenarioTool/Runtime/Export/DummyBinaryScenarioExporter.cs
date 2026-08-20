using System.IO;
using System.Text;

namespace LightingScenarioTool
{
    public sealed class DummyBinaryScenarioExporter : IScenarioExporter
    {
        public void Export(string path, ScenarioData scenario)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            using (var stream = File.Open(path, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(Encoding.ASCII.GetBytes("LSCN-DUMMY"));
                writer.Write(1);
                writer.Write(scenario?.metadata?.scenarioId ?? string.Empty);
                writer.Write(scenario?.metadata?.scenarioName ?? string.Empty);
                writer.Write(scenario?.metadata?.duration ?? 0f);
                writer.Write(scenario?.lightingUnits?.Count ?? 0);
            }
        }
    }
}
