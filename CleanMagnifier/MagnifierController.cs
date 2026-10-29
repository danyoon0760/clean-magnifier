using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using CleanMagnifier.Capture;
using CleanMagnifier.Infrastructure;
using CleanMagnifier.Interop;
using CleanMagnifier.Model;
using CleanMagnifier.Themes;
using CleanMagnifier.Windows;
using Microsoft.Win32;

namespace CleanMagnifier;

/// <summary>
/// 기하 상태·설정·두 창·트레이·캡처 스레드를 묶는 중심 객체. UI 스레드에서만 쓴다.
/// 기하 변경은 두 단계다: <see cref="Preview"/>(배치만) → <see cref="Commit"/>(모니터 안으로 보정 + 저장).
/// </summary>
internal sealed class MagnifierController
{
    // 휠처럼 연속으로 바뀌는 값은 멈춘 뒤 한 번만 저장한다.
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(800) };
    private SourceWindow? _source;
    private ViewerWindow? _viewer;
    private TrayIcon? _tray;
    private CaptureLoop? _capture;
    private LensGeometry? _dragStart;
    private bool _exiting;

    // 뷰어 최대 크기 = 가장 큰 모니터. 드래그 중 매번 모니터를 열거하지 않도록 화면 설정이 바뀔 때만 다시 잰다.
    private SizeD _viewerLimit;

    public AppSettings Settings { get; } = SettingsStore.Load();

    public LensGeometry Geometry { get; private set; }

    public MagnifierController()
    {
        var monitors = Screens.Monitors;
        _viewerLimit = Screens.Largest(monitors);
        Geometry = (Settings.Lens ?? LensGeometry.CreateDefault(Screens.Primary)).FitViewer(_viewerLimit).KeepOnScreens(monitors);
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            Commit(Geometry, showBadge: false);
        };
    }

    public void Start()
    {
        _viewer = new ViewerWindow(this);
        _source = new SourceWindow(this);
        new WindowInteropHelper(_viewer).EnsureHandle();
        _source.Owner = _viewer; // 소유 창은 항상 소유자 위 → 뷰어를 클릭해도 소스가 가려지지 않는다
        new WindowInteropHelper(_source).EnsureHandle();

        _capture = new CaptureLoop(CaptureParams(), _viewer.SetFrame, OnFramePresented);
        _tray = new TrayIcon(this);

        ApplySettings();
        Layout();
        _viewer.Show();
        _source.Show();
        Layout();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;

        if (_viewer.CaptureExclusionFailed || _source.CaptureExclusionFailed)
        {
            var viewer = _viewer;
            Application.Current.Dispatcher.BeginInvoke(() => MessageBox.Show(viewer,
                "이 Windows 에서는 돋보기 창을 화면 캡처에서 뺄 수 없어, 확대 화면에 돋보기 자신이 겹쳐 보일 수 있습니다.\n\n" +
                "Windows 10 2004(20H1) 이상으로 업데이트하면 해결됩니다.",
                "깔끔한 돋보기", MessageBoxButton.OK, MessageBoxImage.Warning));
        }
    }

    public void Exit()
    {
        if (_exiting) return;
        _exiting = true;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        _saveTimer.Stop();
        _capture?.Dispose();
        Save();
        _tray?.Dispose();
        Application.Current.Shutdown();
    }

    // ── 드래그 (LensWindow 가 연결) ─────────────────────────

    public void BeginDrag() => _dragStart = Geometry;

    public void DragTo(LensPart part, Grip grip, double dx, double dy)
    {
        if (_dragStart is { } start) Preview(start.Drag(part, grip, dx, dy, _viewerLimit));
    }

    /// <summary>드래그를 놓으면 모니터 밖·사이 빈 공간에 놓인 사각형을 화면 안으로 당기고 저장한다.</summary>
    public void EndDrag()
    {
        _dragStart = null;
        Commit(Geometry, showBadge: false);
    }

    // ── 메뉴·휠·트레이 ──────────────────────────────────────

    public void ZoomBy(int wheelDelta)
    {
        Preview(Geometry.WithZoom(Geometry.Zoom * Math.Pow(1.1, wheelDelta / 120.0), _viewerLimit));
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void SetZoom(double zoom) => Commit(Geometry.WithZoom(zoom, _viewerLimit));

    public void SetAspect(double aspect) => Commit(Geometry.WithAspect(aspect, _viewerLimit));

    public void ResetLayout() => Commit(LensGeometry.CreateDefault(Screens.Primary));

    /// <summary>가려지거나 화면 밖에 있는 두 창을 보이는 곳으로 꺼내 맨 앞으로 가져온다.</summary>
    public void BringToFront()
    {
        if (_exiting || _source is null || _viewer is null) return;
        Commit(Geometry, showBadge: false);
        _viewer.Show();
        _source.Show();
        _viewer.Activate();
    }

    public void ChangeSettings(Action<AppSettings> change)
    {
        change(Settings);
        ApplySettings();
        Save();
    }

    // ── 기하 상태 ────────────────────────────────────────────

    public void Layout()
    {
        if (_exiting || _source is null || _viewer is null) return;
        _source.Place(Geometry.Source);
        _viewer.Place(Geometry.Viewer);
    }

    /// <summary>새 기하 상태로 창을 옮긴다. 보정·저장은 하지 않는다(드래그·휠 도중).</summary>
    private void Preview(LensGeometry next, bool showBadge = true)
    {
        SetGeometry(next);
        Layout();
        if (showBadge) _viewer?.ShowBadge(BadgeText());
    }

    /// <summary>뷰어를 모니터 크기 이하로, 두 사각형을 모니터 안으로 보정해 확정하고 저장한다.</summary>
    private void Commit(LensGeometry next, bool showBadge = true)
    {
        Preview(next.FitViewer(_viewerLimit).KeepOnScreens(Screens.Monitors), showBadge);
        Save();
    }

    private void SetGeometry(LensGeometry next)
    {
        Geometry = next;
        PushCaptureParams();
    }

    private void Save()
    {
        Settings.Lens = Geometry;
        SettingsStore.Save(Settings);
    }

    private void ApplySettings()
    {
        Theme.SetAccent(Settings.Accent);
        _source?.Apply(Settings);
        _viewer?.Apply(Settings);
        PushCaptureParams();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _viewerLimit = Screens.Largest(Screens.Monitors);
            Commit(Geometry, showBadge: false);
        });

    /// <summary>화면이 잠겨 있는 동안은 볼 사람이 없으니 캡처를 멈춘다.</summary>
    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        bool? paused = e.Reason switch
        {
            SessionSwitchReason.SessionLock => true,
            // 잠금 해제를 놓쳐 영영 멈추는 일이 없도록 다시 연결될 때도 재개한다.
            SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect => false,
            _ => null,
        };
        if (paused is { } p) Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (_capture is not null) _capture.Paused = p;
        });
    }

    private string BadgeText()
    {
        var px = Geometry.Source.ToPixels();
        return $"{Geometry.Zoom:0.0}×  ·  {px.W} × {px.H}";
    }

    // ── 캡처 ────────────────────────────────────────────────

    private CaptureParams CaptureParams() =>
        new(Geometry.Source.ToPixels(), Settings.Fps, Settings.Invert, Settings.FollowMouse);

    private void PushCaptureParams()
    {
        if (_capture is not null) _capture.Params = CaptureParams();
    }

    /// <summary>프레임이 화면에 반영될 때마다(UI 스레드) 호출된다. 마우스 따라가기면 소스 창을 커서로 옮긴다.</summary>
    private void OnFramePresented()
    {
        if (!Settings.FollowMouse || _exiting) return;
        var (cx, cy) = Native.CursorPos();
        var next = Geometry.WithSourceCenteredAt(cx, cy);
        if (next == Geometry) return;
        SetGeometry(next);
        _source?.Place(next.Source);
    }
}
