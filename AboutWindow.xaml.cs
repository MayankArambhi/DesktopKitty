using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace TinyBongo;

/// <summary>
/// Lightweight about dialog — RAM is read once when the window opens.
/// </summary>
public partial class AboutWindow : Window
{
    private const string Author = "Mayank Arambhi";

    public AboutWindow()
    {
        InitializeComponent();
        PopulateInfo();
    }

    private void PopulateInfo()
    {
        VersionValue.Text = GetAppVersion();
        AuthorValue.Text = Author;

        var workingSet = Process.GetCurrentProcess().WorkingSet64;
        RamValue.Text = FormatBytes(workingSet);
        DotNetValue.Text = Environment.Version.ToString(3);
    }

    private static string GetAppVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "1.0.0" : version.ToString(3);
    }

    private static string FormatBytes(long bytes)
    {
        const double mb = 1024 * 1024;
        return $"{bytes / mb:F1} MB";
    }

    private void GitHubLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
