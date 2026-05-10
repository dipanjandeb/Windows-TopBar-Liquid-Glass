using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsBar.Helpers;

namespace WindowsBar;

public partial class MainBar : Window
{
    private const int BAR_HEIGHT = 36;
    private const uint WM_APPBAR_MSG = 0x0401;

    private SystemInfoProvider? _sysInfo;
    private HwndSource? _hwndSource;
    private IntPtr _hwnd;

    private DispatcherTimer? _clockTimer;
    private DispatcherTimer? _windowTitleTimer;
    private DispatcherTimer? _wifiNameTimer;
    private DispatcherTimer? _volumeTimer;

    private bool _appBarRegistered;

    private static readonly char[] LineSplitters = ['\r', '\n'];
    private static readonly char[] ColonSplitter = [':'];

#pragma warning disable SYSLIB1054
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, ref INPUT pInputs, int cbSize);
#pragma warning restore SYSLIB1054

    public MainBar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hwndSource?.AddHook(WndProc);

        ConfigureWindow();
        ApplyWallpaperColors();
        RegisterAsAppBar();
        InitSystemInfo();

        StartClockTimer();
        StartWindowTitleTimer();
        StartWifiNameTimer();
        StartVolumeTimer();

        UpdateClock();
        UpdateWifiName();
        UpdateVolumeUI();
    }

    private void ConfigureWindow()
    {
        int exStyle = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE);
        exStyle |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE, exStyle);

        NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

        NativeMethods.SetDarkMode(_hwnd);
        NativeMethods.EnableAcrylic(_hwnd);

        // WPF Width/Height are in logical units (DIPs) — correct
        Width = SystemParameters.PrimaryScreenWidth;
        Height = BAR_HEIGHT;
        Left = 0;
        Top = 0;
    }

    public void RefreshColors() => ApplyWallpaperColors();

    private void ApplyWallpaperColors()
    {
        try
        {
            var (primary, secondary) = WallpaperColorSampler.SampleWallpaperColors();
            var darkPrimary = WallpaperColorSampler.WithAlpha(BlendWithBlue(primary, 0.4f), 150);
            var darkSecondary = WallpaperColorSampler.WithAlpha(BlendWithBlue(secondary, 0.3f), 110);
            GradTop.Color = darkPrimary;
            GradBot.Color = darkSecondary;
            RootBorder.BorderBrush = new SolidColorBrush(WallpaperColorSampler.Lighten(darkPrimary, 0.3f));
        }
        catch { }
    }

    private static Color BlendWithBlue(Color c, float f) => Color.FromArgb(c.A,
        (byte)(c.R * (1 - f * 0.3f)),
        (byte)(c.G * (1 - f * 0.1f)),
        (byte)Math.Min(255, c.B + (255 - c.B) * f * 0.2f));

    private void RegisterAsAppBar()
    {
        try
        {
            int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
            NativeMethods.RegisterAppBar(_hwnd, BAR_HEIGHT, screenWidth, WM_APPBAR_MSG);
            _appBarRegistered = true;
        }
        catch { }
    }

    private void InitSystemInfo()
    {
        _sysInfo = new SystemInfoProvider();
        _sysInfo.DataUpdated += OnSystemDataUpdated;
    }

    // --- TIMERS ---

    private void StartClockTimer()
    {
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (s, e) => UpdateClock();
        _clockTimer.Start();
    }

    private void StartWindowTitleTimer()
    {
        _windowTitleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
        _windowTitleTimer.Tick += (s, e) => UpdateActiveWindowTitle();
        _windowTitleTimer.Start();
    }

    private void StartWifiNameTimer()
    {
        _wifiNameTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _wifiNameTimer.Tick += (s, e) => UpdateWifiName();
        _wifiNameTimer.Start();
    }

    private void StartVolumeTimer()
    {
        _volumeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _volumeTimer.Tick += (s, e) => UpdateVolumeUI();
        _volumeTimer.Start();
    }

    // --- UI UPDATES ---

    private void UpdateClock()
    {
        var now = DateTime.Now;
        TxtTime.Text = now.ToShortTimeString();
        TxtDay.Text = now.DayOfWeek.ToString()[..3].ToUpper();
        TxtDate.Text = now.ToString("MMM d");
    }

    private void UpdateActiveWindowTitle()
    {
        try
        {
            var fg = GetForegroundWindow();
            if (fg == _hwnd || fg == IntPtr.Zero) return;

            var sb = new StringBuilder(256);
            int charsCopied = GetWindowText(fg, sb, 256);
            var title = charsCopied > 0 ? sb.ToString().Trim() : string.Empty;

            _ = GetWindowThreadProcessId(fg, out uint pid);

            string display = title;
            try
            {
                var proc = Process.GetProcessById((int)pid);
                if (string.IsNullOrEmpty(display))
                    display = proc.ProcessName;
            }
            catch { }

            TxtActiveWindow.Text = string.IsNullOrEmpty(display) ? "Desktop" : display;
        }
        catch { }
    }

    private void UpdateVolumeUI()
    {
        bool isMuted = AudioHelper.IsSystemMuted();
        VolMute.Visibility = isMuted ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateWifiName()
    {
        if (_sysInfo != null && _sysInfo.IsWifi)
        {
            var (ssid, signal) = GetWifiStatus();
            TxtWifiName.Text = ssid;
            TxtWifiName.Visibility = Visibility.Visible;
            UpdateWifiSignalUI(signal);
        }
        else
        {
            TxtWifiName.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateWifiSignalUI(int signal)
    {
        SolidColorBrush brush;

        if (signal > 75)
        {
            brush = new SolidColorBrush(Color.FromRgb(102, 255, 153));
            WifiWave1.Visibility = Visibility.Visible;
            WifiWave2.Visibility = Visibility.Visible;
            WifiWave3.Visibility = Visibility.Visible;
        }
        else if (signal > 40)
        {
            brush = new SolidColorBrush(Color.FromRgb(255, 204, 0));
            WifiWave1.Visibility = Visibility.Visible;
            WifiWave2.Visibility = Visibility.Visible;
            WifiWave3.Visibility = Visibility.Collapsed;
        }
        else
        {
            brush = new SolidColorBrush(Color.FromRgb(255, 102, 102));
            WifiWave1.Visibility = Visibility.Visible;
            WifiWave2.Visibility = Visibility.Collapsed;
            WifiWave3.Visibility = Visibility.Collapsed;
        }

        WifiDot.Stroke = brush;
        WifiWave1.Stroke = brush;
        WifiWave2.Stroke = brush;
        WifiWave3.Stroke = brush;
    }

    private static (string SSID, int Signal) GetWifiStatus()
    {
        string ssid = "Disconnected";
        int signal = 0;
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh.exe",
                    Arguments = "wlan show interfaces",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            foreach (string line in output.Split(LineSplitters, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Contains(" SSID") && !line.Contains("BSSID"))
                {
                    string[] parts = line.Split(ColonSplitter, 2);
                    if (parts.Length == 2) ssid = parts[1].Trim();
                }
                else if (line.Contains(" Signal"))
                {
                    string[] parts = line.Split(ColonSplitter, 2);
                    if (parts.Length == 2)
                    {
                        string sigStr = parts[1].Trim().Replace("%", "");
                        if (!int.TryParse(sigStr, out signal))
                            signal = 0;
                    }
                }
            }
        }
        catch { }

        return (ssid, signal);
    }

    private void OnSystemDataUpdated()
    {
        if (_sysInfo == null) return;

        BatteryPill.Visibility = _sysInfo.HasBattery ? Visibility.Visible : Visibility.Collapsed;
        if (_sysInfo.HasBattery)
        {
            TxtBattery.Text = $"{_sysInfo.BatteryPercent}%";
            TxtCharging.Visibility = _sysInfo.IsCharging ? Visibility.Visible : Visibility.Collapsed;
            BattFill.Width = Math.Max(1, 14.0 * _sysInfo.BatteryPercent / 100.0);
            BattFill.Fill = _sysInfo.BatteryPercent < 20
                ? new SolidColorBrush(Color.FromRgb(255, 80, 80))
                : _sysInfo.BatteryPercent < 40
                    ? new SolidColorBrush(Color.FromRgb(255, 200, 60))
                    : new SolidColorBrush(Color.FromRgb(100, 255, 150));
        }

        if (!_sysInfo.IsNetworkConnected)
        {
            WifiDot.Visibility = Visibility.Collapsed;
            WifiWave1.Visibility = Visibility.Collapsed;
            WifiWave2.Visibility = Visibility.Collapsed;
            WifiWave3.Visibility = Visibility.Collapsed;
            EthIcon.Visibility = Visibility.Collapsed;
            NoNetIcon.Visibility = Visibility.Visible;
            TxtWifiName.Visibility = Visibility.Collapsed;
        }
        else if (_sysInfo.IsWifi)
        {
            WifiDot.Visibility = Visibility.Visible;
            EthIcon.Visibility = Visibility.Collapsed;
            NoNetIcon.Visibility = Visibility.Collapsed;
            TxtWifiName.Visibility = Visibility.Visible;
        }
        else
        {
            WifiDot.Visibility = Visibility.Collapsed;
            WifiWave1.Visibility = Visibility.Collapsed;
            WifiWave2.Visibility = Visibility.Collapsed;
            WifiWave3.Visibility = Visibility.Collapsed;
            EthIcon.Visibility = Visibility.Visible;
            NoNetIcon.Visibility = Visibility.Collapsed;
            TxtWifiName.Visibility = Visibility.Collapsed;
        }
    }

    // --- BUTTON EVENT HANDLERS ---
    private void BarBackground_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    => ApplyWallpaperColors();
    private void BtnStart_Click(object sender, RoutedEventArgs e) => SimulateShortcut(0x5B);

    private void BtnQuickSettings_Click(object sender, MouseButtonEventArgs e) => SimulateShortcut(0x41);

    private void BtnVolume_Click(object sender, RoutedEventArgs e) => SimulateShortcut(0x41);

    private void BtnNotif_Click(object sender, RoutedEventArgs e) => SimulateShortcut(0x4E);

    private static void SimulateShortcut(ushort vkKey)
    {
        var win = new INPUT { type = 1 };
        win.u.ki.wVk = 0x5B;
        _ = SendInput(1, ref win, Marshal.SizeOf(typeof(INPUT)));

        if (vkKey != 0x5B)
        {
            var key = new INPUT { type = 1 };
            key.u.ki.wVk = vkKey;
            _ = SendInput(1, ref key, Marshal.SizeOf(typeof(INPUT)));
            key.u.ki.dwFlags = 0x0002;
            _ = SendInput(1, ref key, Marshal.SizeOf(typeof(INPUT)));
        }

        win.u.ki.dwFlags = 0x0002;
        _ = SendInput(1, ref win, Marshal.SizeOf(typeof(INPUT)));
    }

    private static void SimulateVolumeShortcut()
    {
        var win = new INPUT { type = 1 }; win.u.ki.wVk = 0x5B; _ = SendInput(1, ref win, Marshal.SizeOf(typeof(INPUT)));
        var ctrl = new INPUT { type = 1 }; ctrl.u.ki.wVk = 0x11; _ = SendInput(1, ref ctrl, Marshal.SizeOf(typeof(INPUT)));
        var v = new INPUT { type = 1 }; v.u.ki.wVk = 0x56; _ = SendInput(1, ref v, Marshal.SizeOf(typeof(INPUT)));

        v.u.ki.dwFlags = 0x0002; _ = SendInput(1, ref v, Marshal.SizeOf(typeof(INPUT)));
        ctrl.u.ki.dwFlags = 0x0002; _ = SendInput(1, ref ctrl, Marshal.SizeOf(typeof(INPUT)));
        win.u.ki.dwFlags = 0x0002; _ = SendInput(1, ref win, Marshal.SizeOf(typeof(INPUT)));
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_APPBAR_MSG)
            NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        return IntPtr.Zero;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _clockTimer?.Stop();
        _windowTitleTimer?.Stop();
        _wifiNameTimer?.Stop();
        _volumeTimer?.Stop();
        _sysInfo?.Dispose();
        _hwndSource?.RemoveHook(WndProc);
        if (_appBarRegistered)
            NativeMethods.UnregisterAppBar(_hwnd);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint type; public InputUnion u; }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT { public int dx, dy, mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT { public ushort wVk, wScan, dwFlags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT { public uint uMsg; public ushort wParamL, wParamH; }
}