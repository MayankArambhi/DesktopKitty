using TinyBongo.Models;

namespace TinyBongo.Services;

/// <summary>
/// Derives display statistics from persisted settings on demand — no timers or background work.
/// </summary>
public static class StatisticsCalculator
{
    private const int ActiveHoursPerDay = 8;

    private static readonly long[] Milestones =
    [
        1_000,
        10_000,
        100_000,
        500_000,
        1_000_000,
        5_000_000,
        10_000_000
    ];

    public static UsageStatistics Compute(AppSettings settings, SessionStats session)
    {
        EnsureTodayCurrent(settings);

        var installDate = settings.InstallDate == default
            ? DateTime.UtcNow
            : settings.InstallDate;

        var runningDays = Math.Max(1, (DateTime.UtcNow.Date - installDate.Date).Days + 1);
        var clicks = settings.ClickCount;
        var keyboard = settings.KeyboardClickCount;
        var mouse = settings.MouseClickCount;
        var avgPerDay = clicks / runningDays;
        var avgPerHour = clicks / settings.totalActiveHours;
        var (nextMilestone, progress) = GetMilestoneInfo(clicks);

        return new UsageStatistics
        {
            RunningDays = runningDays,
            LifetimeClicks = clicks,
            KeyboardClicks = keyboard,
            MouseClicks = mouse,
            KeyboardPercentage = PercentOf(clicks, keyboard),
            MousePercentage = PercentOf(clicks, mouse),
            AverageClicksPerDay = avgPerDay,
            AverageActiveClicksPerHour = avgPerHour,
            NextMilestone = nextMilestone,
            MilestoneProgress = progress,
            TodayClicks = settings.TodayClicks,
            SessionClicks = session.SessionClicks,
            SessionDuration = DateTime.UtcNow - session.SessionStartUtc
        };
    }

    /// <summary>Resets today's counter when the UTC date rolls over — called on click and refresh.</summary>
    public static bool EnsureTodayCurrent(AppSettings settings)
    {
        var today = DateTime.UtcNow.Date;
        if (settings.TodayDate != today)
        {
            settings.TodayDate = today;
            settings.TodayClicks = 0;
            return true;
        }

        return false;
    }

    public static (long Next, double Progress) GetMilestoneInfo(long clicks)
    {
        long next = 0;
        foreach (var milestone in Milestones)
        {
            if (clicks < milestone)
            {
                next = milestone;
                break;
            }
        }

        if (next == 0)
        {
            next = ((clicks / 10_000_000L) + 1) * 10_000_000L;
        }

        var progress = next > 0 ? Math.Clamp((double)clicks / next, 0, 1) : 0;
        return (next, progress);
    }

    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        }

        if (duration.TotalMinutes >= 1)
        {
            return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
        }

        return $"{Math.Max(0, duration.Seconds)}s";
    }

    private static int PercentOf(long total, long part)
    {
        if (total <= 0)
        {
            return 0;
        }

        return (int)Math.Round(part * 100.0 / total);
    }
}
