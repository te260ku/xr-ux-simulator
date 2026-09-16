using System;
using System.Reflection;

namespace App.Timeline
{
    public sealed class TimelineCommandDescriptor
    {
        public string Id { get; }
        public object Target { get; }
        public MethodInfo Method { get; }
        public TimelineCommandSchema Schema { get; }

        public TimelineCommandDescriptor(
            string id,
            object target,
            MethodInfo method,
            TimelineCommandSchema schema)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Target = target;
            Method = method ?? throw new ArgumentNullException(nameof(method));
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
        }
    }
}
