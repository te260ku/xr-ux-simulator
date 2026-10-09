using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using UnityEngine;

public static class TimelineCommandDefinitionMenu
{
    [MenuItem("Tools/Timeline/Export Command Definitions")]
    private static void Export()
    {
        var path = EditorUtility.SaveFilePanel(
            "Export Timeline Command Definitions",
            "",
            "command-definitions.json",
            "json");

        if (string.IsNullOrWhiteSpace(path))
            return;

        var resolver =
            new CamelCasePropertyNamesContractResolver();

        var exporter =
            new TimelineCommandDefinitionExporter(
                new TimelineCommandScanner(),
                resolver);

        var document = exporter.CreateDefinitions();

        var settings = new JsonSerializerSettings
        {
            ContractResolver = resolver,
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented
        };

        File.WriteAllText(
            path,
            JsonConvert.SerializeObject(document, settings));

        Debug.Log(
            $"Timeline command definitions exported: {path}");
    }
}
