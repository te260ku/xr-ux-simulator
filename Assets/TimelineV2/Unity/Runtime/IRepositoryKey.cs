namespace App.Timeline.V2
{
    /// <summary>
    /// Common contract for IDs used as repository lookup keys.
    /// Implementations must expose a public constructor that accepts one string.
    /// </summary>
    public interface IRepositoryKey
    {
        string Value { get; }
    }
}
