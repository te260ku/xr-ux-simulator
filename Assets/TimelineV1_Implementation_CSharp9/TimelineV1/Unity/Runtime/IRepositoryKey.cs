namespace App.Timeline
{
    /// <summary>
    /// Marker contract for strongly typed keys used to identify entries in repositories.
    /// Implementations must expose a public constructor that accepts one string.
    /// </summary>
    public interface IRepositoryKey
    {
        string Value { get; }
    }
}
