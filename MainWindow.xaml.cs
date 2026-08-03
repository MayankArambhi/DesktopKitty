using System.IO;
using System.Windows;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using TinyBongo.Models;
using TinyBongo.Services;

namespace TinyBongo;

/// <summary>
/// Transparent always-on-top overlay that displays cached cat sprites
/// and reacts to global keyboard/mouse input.
/// </summary>
public partial class MainWindow : Window
{
    private enum CatState
    {
        Idle,
        LeftPaw,
        RightPaw,
        BothPaws,
        MouseClick
    }

    private enum KeySide
    {
        None,
        Left,
        Right,
        Both
    }

    private const int BaseWidth = 256;
    private const int BaseHeight = 256;
    // No fixed timers for keyboard input — state is driven by key/mouse events.
    private const double MinScale = 0.2;
    private const double MaxScale = 2.5;
    private const double ScaleStep = 0.1;

    // Comprehensive keyboard mapping per design requirements.
    private static readonly HashSet<int> LeftKeys = new()
    {
        // Esc, `, 1-5
        0x1B, 0xC0, 0x31, 0x32, 0x33, 0x34, 0x35,
        // Tab, Q W E R T
        0x09, 0x51, 0x57, 0x45, 0x52, 0x54,
        // CapsLock, A S D F G
        0x14, 0x41, 0x53, 0x44, 0x46, 0x47,
        // LeftShift, Z X C V B
        0xA0, 0x5A, 0x58, 0x43, 0x56, 0x42,
        // LeftCtrl, LeftWindows, LeftAlt
        0xA2, 0x5B, 0xA4,
        // Function keys F1-F5
        0x70, 0x71, 0x72, 0x73, 0x74,
        // Numpad 0,1,4,7
        0x60, 0x61, 0x64, 0x67
    };

    private static readonly HashSet<int> RightKeys = new()
    {
        // 6-0, -, =
        0x36, 0x37, 0x38, 0x39, 0x30, 0xBD, 0xBB,
        // Y U I O P [ ]
        0x59, 0x55, 0x49, 0x4F, 0x50, 0xDB, 0xDD,
        // H J K L ; '
        0x48, 0x4A, 0x4B, 0x4C, 0xBA, 0xDE,
        // N M , . /
        0x4E, 0x4D, 0xBC, 0xBE, 0xBF,
        // RightShift, RightAlt, Menu, RightCtrl
        0xA1, 0xA5, 0x5D, 0xA3,
        // Include Enter and Backspace as right-side
        0x0D, 0x08,
        // Function keys F6-F12
        0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x7B,
        // Navigation keys (treated as right-side)
        0x2D, 0x2E, 0x24, 0x23, 0x21, 0x22, 0x25, 0x26, 0x27, 0x28,
        // Numpad 2,3,5,6,8,9, Decimal, Add, Subtract, Multiply, Divide
        0x62, 0x63, 0x65, 0x66, 0x68, 0x69, 0x6E, 0x6B, 0x6D, 0x6A, 0x6F
    };

    // Special both-hand keys (only Space remains both-handed).
    private static readonly HashSet<int> BothHandKeys = new() { 0x20 }; // Space

    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly InputHookService _inputHookService;
    private readonly StartupService _startupService;
    private readonly Dictionary<CatState, BitmapImage> _sprites = new();

    // Runtime input state
    private readonly HashSet<int> _pressedKeys = new();
    private bool _mouseDown;
    private CatState _currentState = CatState.Idle;
    private long _clickCount;
    private long _sessionClickCount;
    private readonly DateTime _sessionStartUtc = DateTime.UtcNow;
    private bool _isShuttingDown;
    private StatisticsWindow? _statisticsWindow;

    public event Action? ClickCountChanged;

    public MainWindow(AppSettings settings, SettingsService settingsService, InputHookService inputHookService, StartupService startupService)
    {
        InitializeComponent();

        _settings = settings;
        _settingsService = settingsService;
        _inputHookService = inputHookService;
        _startupService = startupService;

        // No timers for keyboard state; input is entirely event-driven.

        LoadSprites();
        ApplySettings();
        SyncContextMenuState();

        // Initialize click counter from settings and subscribe to counted events.
        _clickCount = _settings.ClickCount;
        CounterText.Text = $"{_clickCount}";

        _inputHookService.KeyDown += OnGlobalKeyDown;
        _inputHookService.KeyUp += OnGlobalKeyUp;
        _inputHookService.MouseDown += OnGlobalMouseDown;
        _inputHookService.MouseUp += OnGlobalMouseUp;
        _inputHookService.InputCounted += OnInputCounted;

        // Ensure WPF-level flag and extended styles hide from taskbar
        ShowInTaskbar = false;

        SourceInitialized += (_, _) => ApplyExtendedWindowStyles();
        LocationChanged += (_, _) => PersistWindowPosition();
        Loaded += (_, _) => {
            try { EnsureOnScreen(); } catch { }
        };
        Closing += (_, _) => SaveSettings();
    }

    public void RequestShutdown()
    {
        _isShuttingDown = true;
        Close();
    }

    /// <summary>
    /// Ensure the overlay is on-screen (move to primary center if needed).
    /// Public so external callers (tray) can force the window into view.
    /// </summary>
    public void EnsureOnScreen()
    {
        var screenWidth = SystemParameters.VirtualScreenWidth;
        var screenHeight = SystemParameters.VirtualScreenHeight;

        // If the window would be outside the visible area, center it.
        if (Left < -Width || Top < -Height || Left > screenWidth || Top > screenHeight)
        {
            Left = (screenWidth - Width) / 2.0;
            Top = (screenHeight - Height) / 2.0;
            _settings.WindowX = Left;
            _settings.WindowY = Top;
            _settingsService.Save(_settings);
        }
    }

    public void IncreaseScale()
    {
        SetScale(_settings.Scale + ScaleStep);
    }

    public void DecreaseScale()
    {
        SetScale(_settings.Scale - ScaleStep);
    }

    public void SetScale(double scale)
    {
        ApplyScale(Math.Clamp(scale, MinScale, MaxScale));
    }

    public void ToggleClickThrough()
    {
        _settings.ClickThrough = !_settings.ClickThrough;
        ApplyExtendedWindowStyles();
        _settingsService.Save(_settings);
    }

    public void ToggleAlwaysOnTop()
    {
        _settings.AlwaysOnTop = !_settings.AlwaysOnTop;
        Topmost = _settings.AlwaysOnTop;
        _settingsService.Save(_settings);
    }

    public void ToggleCounterVisibility()
    {
        _settings.ShowCounter = !_settings.ShowCounter;
        ApplyCounterVisibility();
        SyncContextMenuState();
        _settingsService.Save(_settings);
    }

    public void ResetCounter()
    {
        _clickCount = 0;
        _sessionClickCount = 0;
        _settings.ClickCount = 0;
        _settings.KeyboardClickCount = 0;
        _settings.MouseClickCount = 0;
        _settings.TodayClicks = 0;
        _settings.TodayDate = DateTime.UtcNow.Date;
        CounterText.Text = "0";
        _settingsService.Save(_settings);
        NotifyClickCountChanged();
    }

    public void ShowStatistics()
    {
        if (_statisticsWindow is { IsLoaded: true })
        {
            _statisticsWindow.RefreshStatistics();
            _statisticsWindow.Activate();
            return;
        }

        _statisticsWindow = new StatisticsWindow(_settings, GetSessionStats);
        _statisticsWindow.Closed += (_, _) => _statisticsWindow = null;
        _statisticsWindow.Show();
    }

    private SessionStats GetSessionStats() => new()
    {
        SessionClicks = _sessionClickCount,
        SessionStartUtc = _sessionStartUtc
    };

    public void ToggleStartWithWindows()
    {
        _settings.StartWithWindows = !_settings.StartWithWindows;
        _startupService.SetEnabled(_settings.StartWithWindows);
        SyncContextMenuState();
        _settingsService.Save(_settings);
    }

    public void SyncContextMenuState()
    {
        ToggleCounterMenuItem.Header = _settings.ShowCounter ? "Hide Counter" : "Show Counter";
        StartWithWindowsMenuItem.IsChecked = _settings.StartWithWindows;
    }

    private void LoadSprites()
    {
        try
        {
            var assetsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Cat");

            _sprites[CatState.Idle] = LoadSprite(Path.Combine(assetsDir, "idle.png"));
            _sprites[CatState.LeftPaw] = LoadSprite(Path.Combine(assetsDir, "left_paw.png"));
            _sprites[CatState.RightPaw] = LoadSprite(Path.Combine(assetsDir, "right_paw.png"));
            _sprites[CatState.BothPaws] = LoadSprite(Path.Combine(assetsDir, "both_paws.png"));
            _sprites[CatState.MouseClick] = LoadSprite(Path.Combine(assetsDir, "mouse_click.png"));

            SetCatState(CatState.Idle);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to load cat sprites:\n{ex.Message}", "Desktop Kitty", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static BitmapImage LoadSprite(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing cat sprite: {path}");
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void ApplySettings()
    {
        ApplyCounterVisibility();

        // Ensure the window is positioned on-screen. If stored coordinates are
        // outside the current virtual screen (multiple monitors, resolution change),
        // move the window to the primary screen center and persist the new values.
        var screenWidth = SystemParameters.VirtualScreenWidth;
        var screenHeight = SystemParameters.VirtualScreenHeight;

        var desiredLeft = _settings.WindowX;
        var desiredTop = _settings.WindowY;

        // If stored values would place the window fully off-screen, center it.
        if (double.IsNaN(desiredLeft) || double.IsNaN(desiredTop)
            || desiredLeft < -Width || desiredTop < -Height
            || desiredLeft > screenWidth || desiredTop > screenHeight)
        {
            Left = (screenWidth - Width) / 2.0;
            Top = (screenHeight - Height) / 2.0;
            _settings.WindowX = Left;
            _settings.WindowY = Top;
            _settingsService.Save(_settings);
        }
        else
        {
            Left = desiredLeft;
            Top = desiredTop;
        }

        Topmost = _settings.AlwaysOnTop;
        SyncContextMenuState();
    }

    private const int CounterAreaHeight = 42;

    private void ApplyScale(double scale)
    {
        _settings.Scale = scale;

        CatImage.Width = BaseWidth * scale;
        CatImage.Height = BaseHeight * scale;

        _settingsService.Save(_settings);
    }

    private void ApplyCounterVisibility()
    {
        CounterBorder.Visibility = _settings.ShowCounter ? Visibility.Visible : Visibility.Collapsed;
        ApplyScale(_settings.Scale);
    }

    private void ApplyExtendedWindowStyles()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var style = NativeMethods.GetWindowLong(handle, NativeMethods.GwlExstyle);
        // Ensure layered and toolwindow styles are set, and appwindow is cleared
        style |= NativeMethods.WsExLayered | NativeMethods.WsExToolwindow;
        style &= ~NativeMethods.WsExAppwindow;

        if (_settings.ClickThrough)
        {
            style |= NativeMethods.WsExTransparent;
        }
        else
        {
            style &= ~NativeMethods.WsExTransparent;
        }

        NativeMethods.SetWindowLong(handle, NativeMethods.GwlExstyle, style);
    }

    private void OnGlobalKeyDown(int virtualKey)
    {
        Dispatcher.BeginInvoke(() =>
        {
            LogEvent($"KeyDown {virtualKey}");
            _pressedKeys.Add(virtualKey);
            UpdateCatState();
        });
    }

    private void OnGlobalKeyUp(int virtualKey)
    {
        Dispatcher.BeginInvoke(() =>
        {
            LogEvent($"KeyUp {virtualKey}");
            _pressedKeys.Remove(virtualKey);
            UpdateCatState();
        });
    }

    private void OnGlobalMouseDown()
    {
        Dispatcher.BeginInvoke(() =>
        {
            LogEvent("MouseDown");
            _mouseDown = true;
            UpdateCatState();
        });
    }

    private void OnGlobalMouseUp()
    {
        Dispatcher.BeginInvoke(() =>
        {
            LogEvent("MouseUp");
            _mouseDown = false;
            UpdateCatState();
        });
    }

    private static KeySide GetKeySide(int virtualKey)
    {
        if (BothHandKeys.Contains(virtualKey))
        {
            return KeySide.Both;
        }

        if (LeftKeys.Contains(virtualKey))
        {
            return KeySide.Left;
        }

        if (RightKeys.Contains(virtualKey))
        {
            return KeySide.Right;
        }

        return KeySide.None;
    }

    private void UpdateCatState()
    {
        // Priority: mouse click > keyboard > idle
        if (_mouseDown)
        {
            SetCatState(CatState.MouseClick);
            return;
        }

        var count = _pressedKeys.Count;
        if (count == 0)
        {
            SetCatState(CatState.Idle);
            return;
        }

        if (count >= 3)
        {
            SetCatState(CatState.BothPaws);
            return;
        }

        // 1 or 2 keys
        var sides = _pressedKeys.Select(GetKeySide).ToArray();
        if (count == 1)
        {
            var s = sides[0];
            if (s == KeySide.Both)
            {
                SetCatState(CatState.BothPaws);
            }
            else if (s == KeySide.Left)
            {
                SetCatState(CatState.LeftPaw);
            }
            else if (s == KeySide.Right)
            {
                SetCatState(CatState.RightPaw);
            }
            else
            {
                SetCatState(CatState.Idle);
            }
            return;
        }

        // exactly 2
        var first = sides[0];
        var second = sides[1];
        if (first == KeySide.Both || second == KeySide.Both)
        {
            SetCatState(CatState.BothPaws);
            return;
        }

        if (first == KeySide.Left && second == KeySide.Left)
        {
            SetCatState(CatState.LeftPaw);
            return;
        }

        if (first == KeySide.Right && second == KeySide.Right)
        {
            SetCatState(CatState.RightPaw);
            return;
        }

        SetCatState(CatState.BothPaws);
    }

    private void OnInputCounted(InputSource source)
    {
        Dispatcher.BeginInvoke(() =>
        {
            StatisticsCalculator.EnsureTodayCurrent(_settings);

            _clickCount++;
            _sessionClickCount++;
            _settings.ClickCount = _clickCount;
            _settings.TodayClicks++;

            if (source == InputSource.Keyboard)
            {
                _settings.KeyboardClickCount++;
            }
            else
            {
                _settings.MouseClickCount++;
            }

            CounterText.Text = $"{_clickCount}";
            _settingsService.Save(_settings);
            NotifyClickCountChanged();
        });
    }

    private void NotifyClickCountChanged()
    {
        ClickCountChanged?.Invoke();
        _statisticsWindow?.RefreshStatistics();
    }

    private void ToggleCounterMenuItem_Click(object sender, RoutedEventArgs e) => ToggleCounterVisibility();

    private void StatisticsMenuItem_Click(object sender, RoutedEventArgs e) => ShowStatistics();

    private void ResetCounter_Click(object sender, RoutedEventArgs e) => ResetCounter();

    private void StartWithWindowsMenuItem_Click(object sender, RoutedEventArgs e) => ToggleStartWithWindows();

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => RequestShutdown();

    private void SetCatState(CatState state)
    {
        if (_currentState == state)
        {
            return;
        }

        LogEvent($"StateChange {_currentState} -> {state}");
        _currentState = state;
        CatImage.Source = _sprites[state];
    }

    private void LogEvent(string text)
    {
        try
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_events.log");
            File.AppendAllText(logPath, $"{DateTime.Now:O} \t {text}\r\n");
        }
        catch
        {
            // Ignore logging failures
        }
    }

    // No idle timer restart — keyboard state is event-driven; mouse release returns
    // immediately to keyboard state.

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_settings.ClickThrough)
        {
            DragMove();
        }
    }

    private void PersistWindowPosition()
    {
        if (_isShuttingDown)
        {
            return;
        }

        _settings.WindowX = Left;
        _settings.WindowY = Top;
        _settingsService.Save(_settings);
    }

    private void SaveSettings()
    {
        _settings.WindowX = Left;
        _settings.WindowY = Top;
        _settings.IsVisible = Visibility == Visibility.Visible;
        _settingsService.Save(_settings);
    }
}
