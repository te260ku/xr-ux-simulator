using System;
using System.Collections.Generic;
using System.Reflection;

namespace App.Timeline
{
    public sealed class TimelineObjectFactory
    {
        public object Create(
            TimelineParameterSchema schema,
            IReadOnlyList<object> childValues)
        {
            if (schema == null) throw new ArgumentNullException(nameof(schema));
            if (childValues == null) throw new ArgumentNullException(nameof(childValues));
            if (schema.Kind != TimelineParameterKind.Complex)
                throw new ArgumentException("Schema must represent a complex parameter.", nameof(schema));
            if (schema.Children.Count != childValues.Count)
                throw new ArgumentException("Child value count does not match the schema.", nameof(childValues));

            var actualType = Nullable.GetUnderlyingType(schema.Type) ?? schema.Type;

            if (schema.Constructor != null && schema.Constructor.GetParameters().Length > 0)
                return schema.Constructor.Invoke(ToArray(childValues));

            var instance = Activator.CreateInstance(actualType);
            if (instance == null)
                throw new InvalidOperationException($"Could not create timeline parameter type '{actualType.FullName}'.");

            for (var i = 0; i < schema.Children.Count; i++)
            {
                var member = schema.Children[i].BindingMember;
                if (member == null)
                    throw new InvalidOperationException(
                        $"No writable member binding exists for '{schema.Children[i].MemberPath}'.");

                switch (member)
                {
                    case PropertyInfo property:
                        property.SetValue(instance, childValues[i]);
                        break;
                    case FieldInfo field:
                        field.SetValue(instance, childValues[i]);
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported binding member '{member.MemberType}'.");
                }
            }

            return instance;
        }

        private static object[] ToArray(IReadOnlyList<object> values)
        {
            var result = new object[values.Count];
            for (var i = 0; i < values.Count; i++) result[i] = values[i];
            return result;
        }
    }
}
