using System.Windows;
using System.Windows.Input;
using CleanMagnifier.Interop;
using CleanMagnifier.Model;
using static CleanMagnifier.Controls.ChromeLayout;

namespace CleanMagnifier.Controls;

/// <summary>
/// 창 표면에서 변/꼭짓점/내부를 판정해 커서를 바꾸고, 드래그를 이벤트로 알린다.
/// 기하 계산은 하지 않는다. 이동량은 물리 픽셀(GetCursorPos)로 재서 DPI가 섞여도 정확하다.
/// </summary>
internal sealed class LensChrome
{
    private readonly FrameworkElement _surface;
    private readonly double _margin;

    private Grip _grip;
    private (int X, int Y) _start;
    private bool _dragging;

    public bool Enabled { get; set; } = true;

    public event Action? DragStarted;

    /// <summary>잡은 부위와, 드래그 시작점 대비 커서 이동량(물리 픽셀).</summary>
    public event Action<Grip, int, int>? Dragged;

    public event Action? DragEnded;

    /// <param name="margin">표면 가장자리에서 보이는 사각형까지의 여백(DIP).</param>
    public LensChrome(FrameworkElement surface, double margin)
    {
        _surface = surface;
        _margin = margin;

        surface.MouseLeftButtonDown += OnDown;
        surface.MouseMove += OnMove;
        surface.MouseLeftButtonUp += (_, _) => End();
        surface.LostMouseCapture += (_, _) => End();
    }

    private Grip HitTest(Point p)
    {
        var r = new Rect(_margin, _margin,
            Math.Max(0, _surface.ActualWidth - 2 * _margin),
            Math.Max(0, _surface.ActualHeight - 2 * _margin));

        var outer = r;
        outer.Inflate(HitBand, HitBand);
        if (!outer.Contains(p)) return Grip.None;

        // 사각형이 작을 때도 안쪽 이동 영역이 남도록 띠와 꼭짓점 범위를 줄인다.
        double small = Math.Min(r.Width, r.Height);
        double band = Math.Min(HitBand, small / 4);
        double cz = Math.Min(CornerZone, small / 3);

        bool l = p.X <= r.Left + band, rt = p.X >= r.Right - band;
        bool t = p.Y <= r.Top + band, b = p.Y >= r.Bottom - band;
        if (!(l || rt || t || b)) return Grip.Move;

        bool lz = p.X <= r.Left + cz, rz = p.X >= r.Right - cz;
        bool tz = p.Y <= r.Top + cz, bz = p.Y >= r.Bottom - cz;
        if (tz && lz) return Grip.TopLeft;
        if (tz && rz) return Grip.TopRight;
        if (bz && lz) return Grip.BottomLeft;
        if (bz && rz) return Grip.BottomRight;
        if (l) return Grip.Left;
        if (rt) return Grip.Right;
        return t ? Grip.Top : Grip.Bottom;
    }

    private static Cursor CursorFor(Grip g) => g switch
    {
        Grip.Left or Grip.Right => Cursors.SizeWE,
        Grip.Top or Grip.Bottom => Cursors.SizeNS,
        Grip.TopLeft or Grip.BottomRight => Cursors.SizeNWSE,
        Grip.TopRight or Grip.BottomLeft => Cursors.SizeNESW,
        Grip.Move => Cursors.SizeAll,
        _ => Cursors.Arrow,
    };

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        if (!Enabled) return;
        _grip = HitTest(e.GetPosition(_surface));
        if (_grip == Grip.None) return;

        _start = Native.CursorPos();
        _dragging = true;
        _surface.CaptureMouse();
        DragStarted?.Invoke();
        e.Handled = true;
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_dragging)
        {
            var (x, y) = Native.CursorPos();
            Dragged?.Invoke(_grip, x - _start.X, y - _start.Y);
            return;
        }
        _surface.Cursor = Enabled ? CursorFor(HitTest(e.GetPosition(_surface))) : null;
    }

    private void End()
    {
        if (!_dragging) return;
        _dragging = false;
        if (_surface.IsMouseCaptured) _surface.ReleaseMouseCapture();
        DragEnded?.Invoke();
    }
}
