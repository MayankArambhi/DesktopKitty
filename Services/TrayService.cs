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

        var showHide = new ToolStripMenuItem(_settings.IsVisible ? "Hide" : "Show");
        showHide.Click += (_, _) => ToggleVisibility(showHide);

        var increaseSize = new ToolStripMenuItem("Increase Size");
        increaseSize.Click += (_, _) => _mainWindow.IncreaseScale();

        var decreaseSize = new ToolStripMenuItem("Decrease Size");
        decreaseSize.Click += (_, _) => _mainWindow.DecreaseScale();

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

        menu.Items.Add(showHide);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(increaseSize);
        menu.Items.Add(decreaseSize);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(clickThrough);
        menu.Items.Add(alwaysOnTop);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exit);

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => ToggleVisibility(showHide);
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

    private void ToggleVisibility(ToolStripMenuItem showHideItem)
    {
        if (_settings.IsVisible)
        {
            _mainWindow.Hide();
            _settings.IsVisible = false;
            showHideItem.Text = "Show";
        }
        else
        {
                // Ensure the overlay is on-screen before showing it.
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
            showHideItem.Text = "Hide";
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
