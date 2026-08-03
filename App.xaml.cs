using System.Windows;
using TinyBongo.Models;
using TinyBongo.Services;

namespace TinyBongo;

public partial class App : System.Windows.Application
{
    private InputHookService? _inputHookService;
    private TrayService? _trayService;
    private SettingsService? _settingsService;
    private StartupService? _startupService;
    private AppSettings? _settings;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Keep running in the tray after the overlay is closed from the title bar (there isn't one).
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _settingsService = new SettingsService();
        _settings = _settingsService.Load();
        _startupService = new StartupService();
        _inputHookService = new InputHookService();

        // Sync startup registry with persisted preference.
        _startupService.SetEnabled(_settings.StartWithWindows);

        _mainWindow = new MainWindow(_settings, _settingsService, _inputHookService, _startupService);
        _trayService = new TrayService(_mainWindow, _settingsService, _settings);

        _mainWindow.Closed += OnMainWindowClosed;

        try
        {
            _inputHookService.Start();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to start global input hooks:\n{ex.Message}",
                "Desktop Kitty",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        if (_settings.IsVisible)
        {
            _mainWindow.Show();
            try
            {
                _mainWindow.EnsureOnScreen();
            }
            catch
            {
                // ignore
            }
        }
    }

    private void OnMainWindowClosed(object? sender, EventArgs e)
    {
        _inputHookService?.Dispose();
        _trayService?.Dispose();
        Shutdown();
    }
}
