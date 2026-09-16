using System;
using System.Collections.Generic;
using System.Reflection;

namespace App.Timeline
{
    public sealed class TimelineParameterSchema
    {
        private static readonly IReadOnlyList<TimelineParameterSchema> EmptyChildren =
            Array.Empty<TimelineParameterSchema>();

        public string Name { get; }
        public string MemberPath { get; }
        public Type Type { get; }
        public TimelineParameterKind Kind { get; }
        public bool Required { get; }
        public object DefaultValue { get; }
        public string OptionSource { get; }
        public IReadOnlyList<TimelineParameterSchema> Children { get; }

        internal ConstructorInfo Constructor { get; }
        internal MemberInfo BindingMember { get; }

        internal TimelineParameterSchema(
            string name,
            string memberPath,
            Type type,
            TimelineParameterKind kind,
            bool required,
            object defaultValue,
            string optionSource,
            IReadOnlyList<TimelineParameterSchema> children = null,
            ConstructorInfo constructor = null,
            MemberInfo bindingMember = null)
        {
            Name = name;
            MemberPath = memberPath ?? string.Empty;
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Kind = kind;
            Required = required;
            DefaultValue = defaultValue;
            OptionSource = optionSource ?? string.Empty;
            Children = children ?? EmptyChildren;
            Constructor = constructor;
            BindingMember = bindingMember;
        }
    }
}
