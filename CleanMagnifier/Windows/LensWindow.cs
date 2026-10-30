using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CleanMagnifier.Controls;
using CleanMagnifier.Infrastructure;
using CleanMagnifier.Interop;
using CleanMagnifier.Model;

namespace CleanMagnifier.Windows;

/// <summary>
/// 소스/뷰어 창의 공통 동작: 테두리 없는 투명 창, 캡처 제외, 물리 픽셀 배치,
/// 변·꼭짓점 드래그(<see cref="LensChrome"/>).
/// </summary>
public abstract class LensWindow : Window
{
    /// <summary>디버그용: "1"이면 캡처 제외를 끄고 스크린샷에 창이 보이게 한다.</summary>
    private const string VisibleInCaptureVariable = "CLEANMAGNIFIER_VISIBLE_IN_CAPTURE";

    private const int MaxDpiRelayoutsPerSecond = 3;

    private LensChrome _chrome = null!;
    private long _dpiBurstStart;
    private int _dpiBurstCount;

    internal MagnifierController Controller { get; }

    /// <summary>캡처 제외에 실패했는지. Windows 10 2004 미만이면 실패하고, 확대 화면에 우리 창이 찍힌다.</summary>
    internal bool CaptureExclusionFailed { get; private set; }

    /// <summary>보이는 사각형 바깥 여백(DIP). <see cref="ChromeLayout"/> 의 값.</summary>
    protected abstract double FrameMargin { get; }

    public IntPtr Handle => new WindowInteropHelper(this).Handle;

    internal LensWindow(MagnifierController controller)
    {
        Controller = controller;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        UseLayoutRounding = true;
    }

    /// <summary>XAML 이 만들어진 뒤 하위 창에서 호출: 변·꼭짓점 드래그를 컨트롤러로 연결한다.</summary>
    protected void AttachChrome(FrameworkElement surface, LensPart part)
    {
        _chrome = new LensChrome(surface, FrameMargin);
        _chrome.DragStarted += Controller.BeginDrag;
        _chrome.Dragged += (grip, dx, dy) => Controller.DragTo(part, grip, dx, dy);
        _chrome.DragEnded += Controller.EndDrag;
    }

    /// <summary>false 면 드래그를 받지 않는다.</summary>
    protected bool ChromeEnabled
    {
        get => _chrome.Enabled;
        set => _chrome.Enabled = value;
    }

    internal virtual void Apply(AppSettings settings) => Topmost = settings.Topmost;

    /// <summary>보이는 사각형이 <paramref name="r"/>(물리 픽셀)에 오도록 창을 배치한다.</summary>
    public void Place(RectD r)
    {
        if (Handle == IntPtr.Zero) return;
        int m = (int)Math.Round(FrameMargin * VisualTreeHelper.GetDpi(this).DpiScaleX);
        var px = r.ToPixels();
        Native.SetWindowPos(Handle, IntPtr.Zero, px.X - m, px.Y - m, px.W + 2 * m, px.H + 2 * m,
            Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_NOOWNERZORDER);
    }

    protected void SetClickThrough(bool on)
    {
        if (Handle != IntPtr.Zero) Native.SetExStyle(Handle, Native.WS_EX_TRANSPARENT, on);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (Environment.GetEnvironmentVariable(VisibleInCaptureVariable) != "1"
            && !Native.SetWindowDisplayAffinity(Handle, Native.WDA_EXCLUDEFROMCAPTURE))
        {
            CaptureExclusionFailed = true;
            ErrorLog.Write(new Win32Exception(Marshal.GetLastPInvokeError(), $"{GetType().Name} 를 캡처에서 제외하지 못했습니다."));
        }
        // 작업 표시줄에 없는 창은 Alt+Tab 목록에서도 숨긴다. 도구 창이면 작업 표시줄 버튼도 사라진다.
        if (!ShowInTaskbar) Native.SetExStyle(Handle, Native.WS_EX_TOOLWINDOW, true);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);

        // 배율이 다른 모니터 경계에 걸치면 '배치 → DPI 변경 → 다시 배치'가 반복될 수 있다.
        // 1초 안에 여러 번 반복되면 되돌리기를 멈춰 떨림을 끊는다(다음 드래그 때 다시 맞춰진다).
        long now = Environment.TickCount64;
        if (now - _dpiBurstStart > 1000)
        {
            _dpiBurstStart = now;
            _dpiBurstCount = 0;
        }
        if (++_dpiBurstCount > MaxDpiRelayoutsPerSecond) return;

        // WPF가 권장 크기로 창을 바꾼 뒤, 우리 기하 상태로 다시 맞춘다.
        Dispatcher.BeginInvoke(Controller.Layout);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Controller.Exit();
    }
}
