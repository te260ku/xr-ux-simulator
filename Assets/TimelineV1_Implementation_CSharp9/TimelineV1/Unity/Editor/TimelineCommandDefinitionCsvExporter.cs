#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace App.Timeline.Editor
{
    public static class TimelineCommandDefinitionCsvExporter
    {
        [MenuItem("Tools/Timeline/Export Command Definitions CSV")]
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
                var scanner = new TimelineCommandScanner();
                var schemaBuilder = new TimelineCommandSchemaBuilder();
                var schemas = scanner.Scan()
                    .Select(schemaBuilder.Build)
                    .ToArray();

                var duplicate = schemas
                    .GroupBy(x => x.CommandId, StringComparer.Ordinal)
                    .FirstOrDefault(g => g.Count() > 1);
                if (duplicate != null)
                    throw new InvalidOperationException($"Duplicate TimelineCommand ID '{duplicate.Key}'.");

                var rows = new List<TimelineCommandDefinitionRow>();
                foreach (var schema in schemas)
                    AppendCommandRows(schema, rows);

                var csv = BuildCsv(rows);
                File.WriteAllText(path, "\uFEFF" + csv, new UTF8Encoding(false));
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

        private static void AppendCommandRows(
            TimelineCommandSchema schema,
            ICollection<TimelineCommandDefinitionRow> rows)
        {
            var argIndex = 1;
            foreach (var parameter in schema.Parameters)
                AppendParameter(schema.CommandId, parameter, parameter.Name, rows, ref argIndex);
        }

        private static void AppendParameter(
            string commandId,
            TimelineParameterSchema schema,
            string rootArgName,
            ICollection<TimelineCommandDefinitionRow> rows,
            ref int argIndex)
        {
            if (schema.Kind == TimelineParameterKind.Complex)
            {
                foreach (var child in schema.Children)
                    AppendParameter(commandId, child, rootArgName, rows, ref argIndex);
                return;
            }

            var actualType = Nullable.GetUnderlyingType(schema.Type) ?? schema.Type;
            var isEnum = actualType.IsEnum;
            var isFlags = isEnum && actualType.IsDefined(typeof(FlagsAttribute), false);

            rows.Add(new TimelineCommandDefinitionRow
            {
                Command = commandId,
                ArgIndex = argIndex++,
                ArgName = rootArgName,
                MemberPath = schema.MemberPath,
                Type = GetEditorType(schema, actualType),
                Required = schema.Required,
                Source = schema.OptionSource,
                Default = FormatDefault(schema.DefaultValue),
                Min = string.Empty,
                Max = string.Empty,
                Options = isEnum
                    ? string.Join("|", Enum.GetNames(actualType)
                        .Where(n => !isFlags || !string.Equals(n, "None", StringComparison.Ordinal)))
                    : string.Empty,
                MultiSelect = isFlags
            });
        }

        private static string GetEditorType(TimelineParameterSchema schema, Type actualType)
        {
            if (schema.Kind == TimelineParameterKind.Command) return "Command";
            if (typeof(IRepositoryKey).IsAssignableFrom(actualType)) return "RepositoryKey";
            if (actualType.IsEnum) return "Enum";
            if (actualType == typeof(bool)) return "Bool";
            if (actualType == typeof(float) || actualType == typeof(double) || actualType == typeof(decimal)) return "Float";
            if (actualType == typeof(byte) || actualType == typeof(sbyte) ||
                actualType == typeof(short) || actualType == typeof(ushort) ||
                actualType == typeof(int) || actualType == typeof(uint) ||
                actualType == typeof(long) || actualType == typeof(ulong)) return "Int";
            return "String";
        }

        private static string FormatDefault(object value)
        {
            if (value == null) return string.Empty;
            if (value is bool boolean) return boolean ? "true" : "false";
            if (value is Enum enumValue) return enumValue.ToString().Replace(", ", "|");
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static string BuildCsv(IReadOnlyList<TimelineCommandDefinitionRow> rows)
        {
            var lines = new List<string>
            {
                "Command,ArgIndex,ArgName,MemberPath,Type,Required,Source,Default,Min,Max,Options,MultiSelect"
            };

            foreach (var row in rows
                         .OrderBy(r => r.Command, StringComparer.Ordinal)
                         .ThenBy(r => r.ArgIndex))
            {
                lines.Add(string.Join(",", new[]
                {
                    Escape(row.Command),
                    row.ArgIndex.ToString(CultureInfo.InvariantCulture),
                    Escape(row.ArgName),
                    Escape(row.MemberPath),
                    Escape(row.Type),
                    row.Required ? "true" : "false",
                    Escape(row.Source),
                    Escape(row.Default),
                    Escape(row.Min),
                    Escape(row.Max),
                    Escape(row.Options),
                    row.MultiSelect ? "true" : "false"
                }));
            }

            return string.Join("\r\n", lines);
        }

        private static string Escape(string value)
        {
            value = value ?? string.Empty;
            if (!value.Contains(",") && !value.Contains("\"") &&
                !value.Contains("\r") && !value.Contains("\n"))
                return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
#endif
