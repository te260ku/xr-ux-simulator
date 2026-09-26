using System;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class RepositoryKeySourceAttribute : Attribute
{
    public string Source { get; }

    public RepositoryKeySourceAttribute(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Repository source must not be empty.", nameof(source));

        Source = source;
    }
}
