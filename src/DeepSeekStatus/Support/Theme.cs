using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace DeepSeekStatus.Support;

public static class Theme
{
    private static bool _initialized;
    private static bool _dark;
    private static bool _taskbarDark;

    public static event Action? Changed;

    public static bool IsDark => _dark;

    public static bool IsTaskbarDark => _taskbarDark;

    public static void Init()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _dark = ReadAppLightTheme() is not true;
        _taskbarDark = ReadTaskbarLightTheme() is not true;
        Apply();

        SystemEvents.UserPreferenceChanged += (_, _) => Refresh();
        SystemEvents.DisplaySettingsChanged += (_, _) => Refresh();
    }

    public static void Refresh()
    {
        var appLight = ReadAppLightTheme();
        var taskbarLight = ReadTaskbarLightTheme();
        var dark = appLight is not true;
        var taskbarDark = taskbarLight is not true;
        if (dark == _dark && taskbarDark == _taskbarDark)
        {
            return;
        }

        _dark = dark;
        _taskbarDark = taskbarDark;
        Apply();
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            Changed?.Invoke();
        }
        else
        {
            dispatcher.BeginInvoke(() => Changed?.Invoke());
        }
    }

    private static void Apply()
    {
        var resources = Application.Current?.Resources;
        if (resources is null)
        {
            return;
        }

        resources["PanelBackground"] = Brush(PanelBackground);
        resources["PanelBorder"] = Brush(PanelBorder);
        resources["TextPrimary"] = Brush(TextPrimary);
        resources["TextSecondary"] = Brush(TextSecondary);
        resources["TextTertiary"] = Brush(TextTertiary);
        resources["Separator"] = Brush(Separator);
        resources["ControlTrack"] = Brush(ControlTrack);
        resources["SegmentSelected"] = Brush(SegmentSelected);
        resources["SegmentHover"] = Brush(SegmentHover);
        resources["LinkBrush"] = Brush(LinkBrush);
        resources["AccentSwitch"] = Brush(WhaleTheme.BrandBlue);
    }

    public static Color PanelBackground => _dark
        ? Color.FromRgb(0x25, 0x25, 0x27)
        : Color.FromRgb(0xFA, 0xFA, 0xFC);

    public static Color PanelBorder => _dark
        ? Color.FromArgb(0xFF, 0x40, 0x40, 0x44)
        : Color.FromArgb(0xFF, 0xD8, 0xD8, 0xDE);

    public static Color TextPrimary => _dark
        ? Color.FromRgb(0xF2, 0xF2, 0xF7)
        : Color.FromRgb(0x1D, 0x1D, 0x1F);

    public static Color TextSecondary => _dark
        ? Color.FromRgb(0xA1, 0xA1, 0xA6)
        : Color.FromRgb(0x6E, 0x6E, 0x73);

    public static Color TextTertiary => _dark
        ? Color.FromRgb(0x7C, 0x7C, 0x81)
        : Color.FromRgb(0x8E, 0x8E, 0x93);

    public static Color Separator => _dark
        ? Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF)
        : Color.FromArgb(0x22, 0x00, 0x00, 0x00);

    public static Color ControlTrack => _dark
        ? Color.FromArgb(0x3D, 0xFF, 0xFF, 0xFF)
        : Color.FromArgb(0x28, 0x00, 0x00, 0x00);

    public static Color SegmentSelected => _dark
        ? Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)
        : Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);

    public static Color SegmentHover => _dark
        ? Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)
        : Color.FromArgb(0x18, 0x00, 0x00, 0x00);

    public static Color LinkBrush => WhaleTheme.BrandBlue;

    public static SolidColorBrush Brush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static bool? ReadAppLightTheme() => ReadDword(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                                                           "AppsUseLightTheme");

    private static bool? ReadTaskbarLightTheme() => ReadDword(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                                                               "SystemUsesLightTheme");

    private static bool? ReadDword(string path, string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(path);
            if (key?.GetValue(name) is int value)
            {
                return value != 0;
            }
        }
        catch
        {
        }

        return null;
    }
}
