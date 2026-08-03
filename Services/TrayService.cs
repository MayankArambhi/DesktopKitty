using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using TinyBongo.Models;

namespace TinyBongo.Services;

/// <summary>
/// System-tray icon and context menu for controlling the overlay.
/// </summary>
public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly MainWindow _mainWindow;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly ToolStripMenuItem _showHideItem;
    private readonly ToolStripMenuItem _toggleCounterItem;
    private readonly ToolStripMenuItem _startWithWindowsItem;
    private bool _disposed;

    public TrayService(MainWindow mainWindow, SettingsService settingsService, AppSettings settings)
    {
        _mainWindow = mainWindow;
        _settingsService = settingsService;
        _settings = settings;

        _notifyIcon = new NotifyIcon
        {
            Text = "Desktop Kitty",
            Icon = LoadTrayIcon(),
            Visible = true
        };

        var menu = new ContextMenuStrip();

        _showHideItem = new ToolStripMenuItem(_settings.IsVisible ? "Hide" : "Show");
        _showHideItem.Click += (_, _) => ToggleVisibility();

        _toggleCounterItem = new ToolStripMenuItem(_settings.ShowCounter ? "Hide Counter" : "Show Counter");
        _toggleCounterItem.Click += (_, _) =>
        {
            _mainWindow.ToggleCounterVisibility();
            SyncCounterMenuText();
        };

        var statistics = new ToolStripMenuItem("Statistics...");
        statistics.Click += (_, _) => _mainWindow.ShowStatistics();

        var resetCounter = new ToolStripMenuItem("Reset Counter");
        resetCounter.Click += (_, _) => _mainWindow.ResetCounter();

        _startWithWindowsItem = new ToolStripMenuItem("Start With Windows")
        {
            Checked = _settings.StartWithWindows
        };
        _startWithWindowsItem.Click += (_, _) =>
        {
            _mainWindow.ToggleStartWithWindows();
            _startWithWindowsItem.Checked = _settings.StartWithWindows;
        };

        var sizeLabel = new ToolStripLabel($"Size: {Math.Round(_settings.Scale * 100)}%")
        {
            Margin = new Padding(0, 4, 0, 0)
        };

        var sizeSlider = new TrackBar
        {
            Minimum = 20,
            Maximum = 250,
            Value = (int)Math.Round(_settings.Scale * 100),
            TickStyle = TickStyle.BottomRight,
            TickFrequency = 10,
            SmallChange = 5,
            LargeChange = 20,
            AutoSize = false,
            Width = 140
        };
        sizeSlider.Scroll += (_, _) =>
        {
            var newScale = sizeSlider.Value / 100.0;
            _mainWindow.SetScale(newScale);
            sizeLabel.Text = $"Size: {sizeSlider.Value}%";
        };
        var sizeHost = new ToolStripControlHost(sizeSlider)
        {
            Padding = new Padding(4, 0, 4, 0)
        };

        var clickThrough = new ToolStripMenuItem("Toggle Click Through")
        {
            Checked = _settings.ClickThrough
        };
        clickThrough.Click += (_, _) =>
        {
            _mainWindow.ToggleClickThrough();
            clickThrough.Checked = _settings.ClickThrough;
        };

        var alwaysOnTop = new ToolStripMenuItem("Toggle Always On Top")
        {
            Checked = _settings.AlwaysOnTop
        };
        alwaysOnTop.Click += (_, _) =>
        {
            _mainWindow.ToggleAlwaysOnTop();
            alwaysOnTop.Checked = _settings.AlwaysOnTop;
        };

        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (_, _) => _mainWindow.RequestShutdown();

        menu.Items.Add(_showHideItem);
        menu.Items.Add(_toggleCounterItem);
        menu.Items.Add(statistics);
        menu.Items.Add(resetCounter);
        menu.Items.Add(_startWithWindowsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(sizeLabel);
        menu.Items.Add(sizeHost);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(clickThrough);
        menu.Items.Add(alwaysOnTop);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exit);

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => ToggleVisibility();
    }

    private void SyncCounterMenuText()
    {
        _toggleCounterItem.Text = _settings.ShowCounter ? "Hide Counter" : "Show Counter";
        _mainWindow.SyncContextMenuState();
    }

    private static Icon LoadTrayIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Cat", "idle.png");
            if (File.Exists(iconPath))
            {
                using var bitmap = new Bitmap(iconPath);
                return Icon.FromHandle(bitmap.GetHicon());
            }
        }
        catch
        {
            // Fall back to the default application icon.
        }

        return SystemIcons.Application;
    }

    private void ToggleVisibility()
    {
        if (_settings.IsVisible)
        {
            _mainWindow.Hide();
            _settings.IsVisible = false;
            _showHideItem.Text = "Show";
        }
        else
        {
            try
            {
                _mainWindow.EnsureOnScreen();
            }
            catch
            {
                // ignore
            }

            _mainWindow.Show();
            _mainWindow.Activate();
            _settings.IsVisible = true;
            _showHideItem.Text = "Hide";
        }

        _settingsService.Save(_settings);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
