using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CleanMagnifier.Model;
using CleanMagnifier.Themes;

namespace CleanMagnifier.Windows;

/// <summary>뷰어 우클릭 메뉴. 열 때마다 현재 상태로 다시 만든다.</summary>
internal static class ViewerMenu
{
    private static readonly double[] Zooms = [1.5, 2, 3, 4, 6, 8];

    private static readonly (string Name, double Value)[] Aspects =
    [
        ("1:1", 1.0),
        ("4:3", 4.0 / 3),
        ("16:9", 16.0 / 9),
        ("21:9", 21.0 / 9),
    ];

    /// <summary>하위 메뉴의 선택지 하나: 표시 내용, 현재 선택 여부, 눌렀을 때 동작.</summary>
    private readonly record struct Choice(object Label, bool IsChecked, Action Pick);

    /// <param name="fromTray">트레이에서 열면 맨 위에 "돋보기 앞으로 가져오기"를 넣는다.</param>
    public static void Populate(ContextMenu menu, MagnifierController ctl, bool fromTray = false)
    {
        var g = ctl.Geometry;
        var s = ctl.Settings;

        // 설정 하나를 바꾸는 동작
        Action Set(Action<AppSettings> change) => () => ctl.ChangeSettings(change);

        string aspectName = Aspects.FirstOrDefault(a => Math.Abs(g.Aspect - a.Value) < 0.01).Name ?? "사용자";
        string accentName = AccentColors.All.FirstOrDefault(a => a.Hex == s.Accent).Name ?? "";

        menu.Items.Clear();

        if (fromTray)
        {
            menu.Items.Add(Item("돋보기 앞으로 가져오기", ctl.BringToFront));
            menu.Items.Add(new Separator());
        }

        menu.Items.Add(Choices("배율", $"{g.Zoom:0.0}×",
            Zooms.Select(z => new Choice($"{z:0.#}×", Math.Abs(g.Zoom - z) < 0.05, () => ctl.SetZoom(z)))));

        menu.Items.Add(Choices("화면 비율", aspectName,
            Aspects.Select(a => new Choice(a.Name, a.Name == aspectName, () => ctl.SetAspect(a.Value)))));

        menu.Items.Add(Choices("보간", s.Smooth ? "부드럽게" : "픽셀",
        [
            new Choice("픽셀 그대로", !s.Smooth, Set(x => x.Smooth = false)),
            new Choice("부드럽게", s.Smooth, Set(x => x.Smooth = true)),
        ]));

        menu.Items.Add(Choices("새로고침", $"{s.Fps} fps",
            AppSettings.AllowedFps.Select(f =>new Choice($"{f} fps", s.Fps == f, Set(x => x.Fps = f)))));

        menu.Items.Add(new Separator());

        menu.Items.Add(Item("소스 테두리 표시", Set(x => x.ShowSourceBorder = !x.ShowSourceBorder), s.ShowSourceBorder));
        menu.Items.Add(Item("십자선", Set(x => x.Crosshair = !x.Crosshair), s.Crosshair));
        menu.Items.Add(Item("색 반전", Set(x => x.Invert = !x.Invert), s.Invert));
        menu.Items.Add(Item("마우스 따라가기", Set(x => x.FollowMouse = !x.FollowMouse), s.FollowMouse));

        menu.Items.Add(Choices("테두리 색", accentName,
            AccentColors.All.Select(a => new Choice(ColorLabel(a.Name, a.Hex), a.Hex == s.Accent, Set(x => x.Accent = a.Hex)))));

        menu.Items.Add(new Separator());

        menu.Items.Add(Item("항상 위", Set(x => x.Topmost = !x.Topmost), s.Topmost));
        menu.Items.Add(Item("위치 초기화", ctl.ResetLayout));
        var exit = Item("종료", () => Application.Current.Dispatcher.BeginInvoke(ctl.Exit));
        exit.InputGestureText = "Esc";
        menu.Items.Add(exit);
    }

    /// <summary>오른쪽에 현재 값이 보이는 하위 메뉴.</summary>
    private static MenuItem Choices(string header, string currentValue, IEnumerable<Choice> choices)
    {
        var sub = new MenuItem { Header = header, InputGestureText = currentValue };
        foreach (var c in choices) sub.Items.Add(Item(c.Label, c.Pick, c.IsChecked));
        return sub;
    }

    private static MenuItem Item(object header, Action onClick, bool isChecked = false)
    {
        var item = new MenuItem { Header = header, IsChecked = isChecked };
        item.Click += (_, _) => onClick();
        return item;
    }

    private static StackPanel ColorLabel(string name, string hex)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = Theme.Brush(hex),
            Stroke = new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0)),
            StrokeThickness = 1,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });
        panel.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center });
        return panel;
    }
}
