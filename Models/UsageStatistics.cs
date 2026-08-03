namespace TinyBongo.Models;

/// <summary>
/// Computed usage statistics derived from persisted settings and session state.
/// </summary>
public sealed class UsageStatistics
{
    public int RunningDays { get; init; }
    public long LifetimeClicks { get; init; }
    public long KeyboardClicks { get; init; }
    public long MouseClicks { get; init; }
    public int KeyboardPercentage { get; init; }
    public int MousePercentage { get; init; }
    public long AverageClicksPerDay { get; init; }
    public long AverageActiveClicksPerHour { get; init; }
    public long NextMilestone { get; init; }
    public double MilestoneProgress { get; init; }
    public long TodayClicks { get; init; }
    public long SessionClicks { get; init; }
    public TimeSpan SessionDuration { get; init; }
}
