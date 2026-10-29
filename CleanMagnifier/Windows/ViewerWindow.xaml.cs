using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CleanMagnifier.Controls;
using CleanMagnifier.Model;

namespace CleanMagnifier.Windows;

/// <summary>큰 사각형: 확대된 화면을 보여준다. 우클릭 메뉴와 휠 배율 조절.</summary>
public partial class ViewerWindow : LensWindow
{
    private readonly ImageBrush _brush = new() { Stretch = Stretch.Fill };
    private readonly DispatcherTimer _badgeTimer = new() { Interval = TimeSpan.FromMilliseconds(900) };

    protected override double FrameMargin => ChromeLayout.ViewerMargin;

    internal ViewerWindow(MagnifierController controller) : base(controller)
    {
        InitializeComponent();
        AttachChrome(Surface, LensPart.Viewer);
        Lens.Background = _brush;

        MouseWheel += (_, e) => controller.ZoomBy(e.Delta);

        ContextMenu = new ContextMenu();
        ContextMenuOpening += (_, _) => ViewerMenu.Populate(ContextMenu, controller);

        _badgeTimer.Tick += (_, _) =>
        {
            _badgeTimer.Stop();
            Fade(Badge, 0, 260);
        };
    }

    /// <summary>캡처 크기가 바뀌어 새 비트맵이 만들어졌을 때만 호출된다. 내용 갱신은 비트맵에 직접 쓴다.</summary>
    internal void SetFrame(ImageSource frame) => _brush.ImageSource = frame;

    internal override void Apply(AppSettings s)
    {
        base.Apply(s);
        RenderOptions.SetBitmapScalingMode(Lens, s.Smooth ? BitmapScalingMode.HighQuality : BitmapScalingMode.NearestNeighbor);
        Crosshair.Visibility = s.Crosshair ? Visibility.Visible : Visibility.Collapsed;
    }

    internal void ShowBadge(string text)
    {
        BadgeText.Text = text;
        if (Badge.Opacity < 1) Fade(Badge, 1, 110);
        _badgeTimer.Stop();
        _badgeTimer.Start();
    }

    private static void Fade(UIElement e, double to, int ms) =>
        e.BeginAnimation(OpacityProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)));
}
