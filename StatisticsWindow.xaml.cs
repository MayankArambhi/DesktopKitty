using System.Windows;
using TinyBongo.Models;
using TinyBongo.Services;

namespace TinyBongo;

/// <summary>
/// Lightweight statistics panel — values are computed on open and on counter change only.
/// </summary>
public partial class StatisticsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Func<SessionStats> _getSessionStats;

    public StatisticsWindow(AppSettings settings, Func<SessionStats> getSessionStats)
    {
        InitializeComponent();
        _settings = settings;
        _getSessionStats = getSessionStats;
        RefreshStatistics();
    }

    public void RefreshStatistics()
    {
        var stats = StatisticsCalculator.Compute(_settings, _getSessionStats());

        RunningForValue.Text = $"{stats.RunningDays:N0} days";
        LifetimeClicksValue.Text = FormatCount(stats.LifetimeClicks);
        KeyboardClicksValue.Text = FormatCount(stats.KeyboardClicks);
        MouseClicksValue.Text = FormatCount(stats.MouseClicks);
        KeyboardPercentValue.Text = $"{stats.KeyboardPercentage}%";
        MousePercentValue.Text = $"{stats.MousePercentage}%";
        AveragePerDayValue.Text = FormatCount(stats.AverageClicksPerDay);
        AveragePerHourValue.Text = FormatCount(stats.AverageActiveClicksPerHour);

        var milestonePercent = (int)Math.Round(stats.MilestoneProgress * 100);
        MilestoneValue.Text = $"{FormatCount(stats.LifetimeClicks)} / {FormatCount(stats.NextMilestone)}  ({milestonePercent}%)";
        UpdateMilestoneBar(stats.MilestoneProgress);

        TodayClicksValue.Text = FormatCount(stats.TodayClicks);
        SessionClicksValue.Text = FormatCount(stats.SessionClicks);
        SessionDurationValue.Text = StatisticsCalculator.FormatDuration(stats.SessionDuration);
    }

    private static string FormatCount(long value) => value.ToString("N0");

    private void UpdateMilestoneBar(double progress)
    {
        MilestoneTrack.UpdateLayout();
        var trackWidth = MilestoneTrack.ActualWidth;
        MilestoneFill.Width = trackWidth > 0 ? Math.Max(0, trackWidth * progress) : 0;
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => RefreshStatistics();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow { Owner = this };
        about.ShowDialog();
    }
}
