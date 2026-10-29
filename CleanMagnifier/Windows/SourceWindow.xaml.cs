using System.Windows;
using CleanMagnifier.Controls;
using CleanMagnifier.Model;

namespace CleanMagnifier.Windows;

/// <summary>작은 사각형: 확대할 화면 영역.</summary>
public partial class SourceWindow : LensWindow
{
    protected override double FrameMargin => ChromeLayout.SourceMargin;

    internal SourceWindow(MagnifierController controller) : base(controller)
    {
        InitializeComponent();
        AttachChrome(Surface, LensPart.Source);
    }

    internal override void Apply(AppSettings s)
    {
        base.Apply(s);
        var frame = s.ShowSourceBorder ? Visibility.Visible : Visibility.Hidden;
        Frame.Visibility = frame;
        Halo.Visibility = frame;

        // 마우스 따라가기 중에는 클릭이 아래 앱으로 그대로 통과한다.
        ChromeEnabled = !s.FollowMouse;
        SetClickThrough(s.FollowMouse);
    }
}
