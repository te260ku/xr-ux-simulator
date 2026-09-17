using System;

namespace App.Timeline.V2
{
    /// <summary>
    /// One flattened argument entry used when exporting command_definitions.csv.
    /// </summary>
    public sealed class TimelineArgumentDefinition
    {
        public string RootArgumentName { get; }
        public string MemberPath { get; }
        public Type ValueType { get; }
        public bool Required { get; }
        public object DefaultValue { get; }
        public string OptionSource { get; }
        public bool IsCommand { get; }

        public TimelineArgumentDefinition(
            string rootArgumentName,
            string memberPath,
            Type valueType,
            bool required,
            object defaultValue,
            string optionSource,
            bool isCommand)
        {
            RootArgumentName = rootArgumentName ?? string.Empty;
            MemberPath = memberPath ?? string.Empty;
            ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
            Required = required;
            DefaultValue = defaultValue;
            OptionSource = optionSource ?? string.Empty;
            IsCommand = isCommand;
        }
    }
}
