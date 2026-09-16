using System;
using System.Collections.Generic;

namespace App.Timeline
{
    public sealed class TimelineCommandArgumentBinder
    {
        private const int MaxCommandNestingDepth = 16;

        private readonly TimelineCommandRegistry registry;
        private readonly TimelineArgumentConverter converter;
        private readonly TimelineObjectFactory objectFactory;

        public TimelineCommandArgumentBinder(
            TimelineCommandRegistry registry,
            TimelineArgumentConverter converter,
            TimelineObjectFactory objectFactory)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.converter = converter ?? throw new ArgumentNullException(nameof(converter));
            this.objectFactory = objectFactory ?? throw new ArgumentNullException(nameof(objectFactory));
        }

        public TimelineCommandInvocation Bind(
            string commandId,
            IReadOnlyList<string> rawArguments)
        {
            if (rawArguments == null) throw new ArgumentNullException(nameof(rawArguments));
            var cursor = 0;
            var invocation = BindCommand(commandId, rawArguments, ref cursor, 0);

            for (var i = cursor; i < rawArguments.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(rawArguments[i]))
                {
                    throw new FormatException(
                        $"Command '{commandId}' has an unexpected extra argument at Arg{i + 1}: '{rawArguments[i]}'.");
                }
            }

            return invocation;
        }

        private TimelineCommandInvocation BindCommand(
            string commandId,
            IReadOnlyList<string> rawArguments,
            ref int cursor,
            int depth)
        {
            if (depth > MaxCommandNestingDepth)
                throw new FormatException($"Timeline command nesting exceeds {MaxCommandNestingDepth} levels.");

            var descriptor = registry.Get(commandId);
            var arguments = new List<object>(descriptor.Schema.Parameters.Count);

            foreach (var parameter in descriptor.Schema.Parameters)
                arguments.Add(BindParameter(parameter, rawArguments, ref cursor, depth));

            return new TimelineCommandInvocation(descriptor.Id, arguments);
        }

        private object BindParameter(
            TimelineParameterSchema schema,
            IReadOnlyList<string> rawArguments,
            ref int cursor,
            int depth)
        {
            switch (schema.Kind)
            {
                case TimelineParameterKind.Scalar:
                    return converter.Convert(
                        Consume(rawArguments, ref cursor, schema.MemberPath.Length > 0 ? schema.MemberPath : schema.Name),
                        schema.Type,
                        schema.Required,
                        schema.DefaultValue);

                case TimelineParameterKind.Command:
                {
                    var nestedCommandId = Consume(rawArguments, ref cursor, schema.Name).Trim();
                    if (nestedCommandId.Length == 0)
                    {
                        if (!schema.Required) return null;
                        throw new FormatException($"Nested command ID is required for '{schema.Name}'.");
                    }

                    return BindCommand(nestedCommandId, rawArguments, ref cursor, depth + 1);
                }

                case TimelineParameterKind.Complex:
                {
                    var childValues = new List<object>(schema.Children.Count);
                    foreach (var child in schema.Children)
                        childValues.Add(BindParameter(child, rawArguments, ref cursor, depth));
                    return objectFactory.Create(schema, childValues);
                }

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static string Consume(
            IReadOnlyList<string> rawArguments,
            ref int cursor,
            string parameterName)
        {
            if (cursor >= rawArguments.Count)
            {
                // Missing trailing CSV cells are treated as empty so optional/default values still work.
                cursor++;
                return string.Empty;
            }

            return rawArguments[cursor++];
        }
    }
}
