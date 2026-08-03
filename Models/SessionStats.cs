namespace TinyBongo.Models;

/// <summary>
/// Runtime session counters — not persisted.
/// </summary>
public sealed class SessionStats
{
    public long SessionClicks { get; init; }
    public DateTime SessionStartUtc { get; init; }
}
