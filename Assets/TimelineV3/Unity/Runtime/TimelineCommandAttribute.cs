using System;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class TimelineCommandAttribute : Attribute
{
    public string Id { get; }

    public TimelineCommandAttribute(string id)
    {
        Id = id;
    }
}
