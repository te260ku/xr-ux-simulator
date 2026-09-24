#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using UnityEngine;

namespace TimelineSystem.Editor
{
    public static class TimelineCommandDefinitionExporter
    {
        private static readonly CamelCaseNamingStrategy NamingStrategy =
            new CamelCaseNamingStrategy();

        [MenuItem("Tools/Timeline/Export Command Definitions")]
        public static void ExportFromMenu()
        {
            var outputPath = EditorUtility.SaveFilePanel(
                "Export Timeline Command Definitions",
                Application.dataPath,
                "command_definitions",
                "csv");

            if (string.IsNullOrWhiteSpace(outputPath))
                return;

            Export(outputPath);

            AssetDatabase.Refresh();

            Debug.Log(
                $"Timeline command definitions exported: {outputPath}");
        }

        public static void Export(string outputPath)
        {
            var scanner = new TimelineCommandScanner();

            var rows = scanner
                .Scan()
                .OrderBy(GetCommandId, StringComparer.Ordinal)
                .SelectMany(CreateRows)
                .ToList();

            var csv = new StringBuilder();

            csv.AppendLine(
                "Command,ParameterPath,Type,Source,Options,MultiSelect");

            foreach (var row in rows)
            {
                csv.Append(Escape(row.Command));
                csv.Append(',');
                csv.Append(Escape(row.ParameterPath));
                csv.Append(',');
                csv.Append(Escape(row.Type));
                csv.Append(',');
                csv.Append(Escape(row.Source));
                csv.Append(',');
                csv.Append(Escape(row.Options));
                csv.Append(',');
                csv.Append(Escape(row.MultiSelect ? "true" : "false"));
                csv.AppendLine();
            }

            var directory = Path.GetDirectoryName(outputPath);

            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(
                outputPath,
                csv.ToString(),
                new UTF8Encoding(true));
        }

        private static IEnumerable<CommandDefinitionRow> CreateRows(
            MethodInfo method)
        {
            var commandId = GetCommandId(method);
            var rows = new List<CommandDefinitionRow>();

            foreach (var parameter in method.GetParameters())
            {
                var parameterName =
                    parameter.Name
                    ?? throw new InvalidOperationException(
                        $"Parameter name was not found: {method.Name}");

                AddRows(
                    commandId,
                    parameterName,
                    parameter.ParameterType,
                    rows,
                    new HashSet<Type>());
            }

            return rows;
        }

        private static void AddRows(
            string commandId,
            string path,
            Type type,
            List<CommandDefinitionRow> rows,
            HashSet<Type> typeStack)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (typeof(IRepositoryKey).IsAssignableFrom(type))
            {
                var source =
                    type.GetCustomAttribute<RepositoryKeySourceAttribute>()
                    ?? throw new InvalidOperationException(
                        $"{type.FullName} implements IRepositoryKey but has no " +
                        $"{nameof(RepositoryKeySourceAttribute)}.");

                rows.Add(
                    new CommandDefinitionRow(
                        commandId,
                        path,
                        "RepositoryKey",
                        source.Source,
                        string.Empty,
                        false));

                return;
            }

            if (type.IsEnum)
            {
                rows.Add(
                    new CommandDefinitionRow(
                        commandId,
                        path,
                        "Enum",
                        string.Empty,
                        string.Join("|", Enum.GetNames(type)),
                        type.GetCustomAttribute<FlagsAttribute>() != null));

                return;
            }

            if (TryGetSimpleTypeName(type, out var simpleTypeName))
            {
                rows.Add(
                    new CommandDefinitionRow(
                        commandId,
                        path,
                        simpleTypeName,
                        string.Empty,
                        string.Empty,
                        false));

                return;
            }

            if (typeStack.Contains(type))
            {
                throw new InvalidOperationException(
                    $"Recursive argument type is not supported by the command definition exporter: " +
                    $"{type.FullName}.");
            }

            typeStack.Add(type);

            var properties = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property =>
                    property.CanRead &&
                    property.CanWrite &&
                    property.GetIndexParameters().Length == 0)
                .ToArray();

            if (properties.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Timeline argument type '{type.FullName}' has no editable public properties.");
            }

            foreach (var property in properties)
            {
                var jsonName =
                    NamingStrategy.GetPropertyName(
                        property.Name,
                        false);

                AddRows(
                    commandId,
                    $"{path}.{jsonName}",
                    property.PropertyType,
                    rows,
                    typeStack);
            }

            typeStack.Remove(type);
        }

        private static bool TryGetSimpleTypeName(
            Type type,
            out string typeName)
        {
            if (type == typeof(string))
                typeName = "String";
            else if (type == typeof(bool))
                typeName = "Bool";
            else if (type == typeof(byte))
                typeName = "Byte";
            else if (type == typeof(short))
                typeName = "Short";
            else if (type == typeof(int))
                typeName = "Int";
            else if (type == typeof(long))
                typeName = "Long";
            else if (type == typeof(float))
                typeName = "Float";
            else if (type == typeof(double))
                typeName = "Double";
            else if (type == typeof(decimal))
                typeName = "Decimal";
            else
            {
                typeName = null;
                return false;
            }

            return true;
        }

        private static string GetCommandId(MethodInfo method)
        {
            var attribute =
                method.GetCustomAttribute<TimelineCommandAttribute>()
                ?? throw new InvalidOperationException(
                    $"TimelineCommandAttribute was not found: {method.Name}");

            return attribute.CommandId;
        }

        private static string Escape(string value)
        {
            value = value ?? string.Empty;

            if (!value.Contains(",") &&
                !value.Contains("\"") &&
                !value.Contains("\n") &&
                !value.Contains("\r"))
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private sealed class CommandDefinitionRow
        {
            public string Command { get; }
            public string ParameterPath { get; }
            public string Type { get; }
            public string Source { get; }
            public string Options { get; }
            public bool MultiSelect { get; }

            public CommandDefinitionRow(
                string command,
                string parameterPath,
                string type,
                string source,
                string options,
                bool multiSelect)
            {
                Command = command;
                ParameterPath = parameterPath;
                Type = type;
                Source = source;
                Options = options;
                MultiSelect = multiSelect;
            }
        }
    }
}

#endif
