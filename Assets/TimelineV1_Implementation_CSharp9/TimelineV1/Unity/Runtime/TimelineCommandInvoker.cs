using System;
using System.Reflection;

namespace App.Timeline
{
    public sealed class TimelineCommandInvoker
    {
        private readonly TimelineCommandRegistry registry;

        public TimelineCommandInvoker(TimelineCommandRegistry registry)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public void Invoke(TimelineCommandInvocation invocation)
        {
            if (invocation == null) throw new ArgumentNullException(nameof(invocation));

            var descriptor = registry.Get(invocation.CommandId);
            try
            {
                descriptor.Method.Invoke(descriptor.Target, ToArray(invocation.Arguments));
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                throw new InvalidOperationException(
                    $"TimelineCommand '{invocation.CommandId}' threw an exception.",
                    exception.InnerException);
            }
        }

        private static object[] ToArray(System.Collections.Generic.IReadOnlyList<object> values)
        {
            var result = new object[values.Count];
            for (var i = 0; i < values.Count; i++) result[i] = values[i];
            return result;
        }
    }
}
