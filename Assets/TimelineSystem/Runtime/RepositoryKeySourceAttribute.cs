using System;

namespace TimelineSystem
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class RepositoryKeySourceAttribute : Attribute
    {
        public string Source { get; }

        public RepositoryKeySourceAttribute(string source)
        {
            Source = source;
        }
    }
}
