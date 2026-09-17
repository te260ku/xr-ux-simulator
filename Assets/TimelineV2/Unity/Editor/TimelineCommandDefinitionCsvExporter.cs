#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace App.Timeline.V2.Editor
{
    /// <summary>
    /// Exports command_definitions.csv directly from [TimelineCommand] method signatures.
    /// </summary>
    public static class TimelineCommandDefinitionCsvExporter
    {
        [MenuItem("Tools/Timeline V2/Export Command Definitions CSV")]
        public static void Export()
        {
            var path = EditorUtility.SaveFilePanel(
                "Export Timeline Command Definitions",
                Application.dataPath,
                "command_definitions.csv",
                "csv");

            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                var rows = BuildRows();
                File.WriteAllText(path, "\uFEFF" + BuildCsv(rows), new UTF8Encoding(false));
                AssetDatabase.Refresh();
                Debug.Log($"Timeline command definitions exported: {path}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "Timeline Command Export Failed",
                    exception.Message,
                    "OK");
            }
        }

        private static IReadOnlyList<CommandDefinitionRow> BuildRows()
        {
            var methods = TimelineCommandRegistry.FindCommandMethods();
            var duplicate = methods
                .GroupBy(TimelineCommandRegistry.GetCommandId, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1);

            if (duplicate != null)
                throw new InvalidOperationException($"Duplicate TimelineCommand ID '{duplicate.Key}'.");

            var rows = new List<CommandDefinitionRow>();

            foreach (var method in methods)
            {
                TimelineCommandRegistry.ValidateCommandMethod(method);
                var commandId = TimelineCommandRegistry.GetCommandId(method);
                var definitions = TimelineTypeUtility.GetFlatArgumentDefinitions(method);

                for (var index = 0; index < definitions.Count; index++)
                {
                    var definition = definitions[index];
                    rows.Add(new CommandDefinitionRow
                    {
                        Command = commandId,
                        ArgumentIndex = index + 1,
                        ArgumentName = definition.RootArgumentName,
                        MemberPath = definition.MemberPath,
                        Type = TimelineTypeUtility.GetEditorTypeName(definition),
                        Required = definition.Required,
                        Source = definition.OptionSource,
                        Default = TimelineTypeUtility.FormatDefaultValue(definition.DefaultValue),
                        Options = TimelineTypeUtility.GetEnumOptions(definition.ValueType),
                        MultiSelect = TimelineTypeUtility.IsMultiSelectEnum(definition.ValueType)
                    });
                }
            }

            return rows;
        }

        private static string BuildCsv(IReadOnlyList<CommandDefinitionRow> rows)
        {
            var lines = new List<string>
            {
                "Command,ArgIndex,ArgName,MemberPath,Type,Required,Source,Default,Options,MultiSelect"
            };

            foreach (var row in rows
                         .OrderBy(value => value.Command, StringComparer.Ordinal)
                         .ThenBy(value => value.ArgumentIndex))
            {
                lines.Add(string.Join(",", new[]
                {
                    Escape(row.Command),
                    row.ArgumentIndex.ToString(CultureInfo.InvariantCulture),
                    Escape(row.ArgumentName),
                    Escape(row.MemberPath),
                    Escape(row.Type),
                    row.Required ? "true" : "false",
                    Escape(row.Source),
                    Escape(row.Default),
                    Escape(row.Options),
                    row.MultiSelect ? "true" : "false"
                }));
            }

            return string.Join("\r\n", lines);
        }

        private static string Escape(string value)
        {
            value = value ?? string.Empty;
            if (!value.Contains(",") &&
                !value.Contains("\"") &&
                !value.Contains("\r") &&
                !value.Contains("\n"))
                return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private sealed class CommandDefinitionRow
        {
            public string Command { get; set; }
            public int ArgumentIndex { get; set; }
            public string ArgumentName { get; set; }
            public string MemberPath { get; set; }
            public string Type { get; set; }
            public bool Required { get; set; }
            public string Source { get; set; }
            public string Default { get; set; }
            public string Options { get; set; }
            public bool MultiSelect { get; set; }
        }
    }
}
#endif
