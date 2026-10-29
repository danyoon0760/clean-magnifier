using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace CleanMagnifier.Themes;

internal static class Theme
{
    private const string AccentKey = "AccentBrush";
    private static ResourceDictionary? _colors;
    private static bool? _dark;

    /// <summary>시스템 라이트/다크 설정에 맞는 색 사전을 앱 리소스에 넣는다. 다시 부르면 바뀐 경우에만 교체한다.</summary>
    public static void Apply(Application app)
    {
        bool dark = IsSystemDark();
        if (dark == _dark) return;
        _dark = dark;

        var next = new ResourceDictionary
        {
            Source = new Uri($"Themes/Colors.{(dark ? "Dark" : "Light")}.xaml", UriKind.Relative),
        };
        var merged = app.Resources.MergedDictionaries;
        if (_colors is not null) merged.Remove(_colors);
        merged.Insert(0, next);
        _colors = next;
    }

    /// <summary>실행 중에 Windows 테마를 바꾸면 따라간다.</summary>
    public static void Watch(Application app) =>
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color)
                app.Dispatcher.BeginInvoke(() => Apply(app));
        };

    public static void SetAccent(string hex) => Application.Current.Resources[AccentKey] = Brush(hex);

    public static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    private static bool IsSystemDark()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
    }
}
