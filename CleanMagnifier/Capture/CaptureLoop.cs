using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CleanMagnifier.Infrastructure;
using CleanMagnifier.Interop;
using CleanMagnifier.Model;

namespace CleanMagnifier.Capture;

/// <summary>캡처 스레드가 읽는 값. 바뀔 때마다 새 인스턴스로 통째로 교체한다.</summary>
internal sealed record CaptureParams(PixelRect Rect, int Fps, bool Invert, bool FollowMouse);

/// <summary>
/// 전용 스레드에서 설정한 fps 로 화면을 캡처하고, 최신 프레임만 UI 스레드로 넘겨 비트맵에 쓴다.
/// UI 스레드는 메모리 복사만 하므로 큰 영역을 캡처해도 드래그·메뉴가 끊기지 않는다.
/// </summary>
internal sealed class CaptureLoop : IDisposable
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly Action<ImageSource> _onBitmapChanged;
    private readonly Action _onPresented;
    private readonly Thread _thread;
    private readonly object _gate = new();

    private CaptureParams _params;
    private volatile bool _running = true;
    private readonly ManualResetEventSlim _active = new(true); // 꺼져 있으면 캡처 스레드가 잠든다

    // _gate 로 보호: 캡처 스레드가 쓰고 UI 스레드가 읽는 최신 프레임
    private byte[] _frame = [];
    private int _frameWidth, _frameHeight;
    private bool _frameReady;

    private int _presentQueued;
    private WriteableBitmap? _bitmap; // UI 스레드 전용

    public CaptureLoop(CaptureParams initial, Action<ImageSource> onBitmapChanged, Action onPresented)
    {
        _params = initial;
        _onBitmapChanged = onBitmapChanged;
        _onPresented = onPresented;
        _thread = new Thread(Run) { IsBackground = true, Name = "CleanMagnifier.Capture" };
        _thread.Start();
    }

    public CaptureParams Params
    {
        get => Volatile.Read(ref _params);
        set => Volatile.Write(ref _params, value);
    }

    /// <summary>true 면 캡처를 멈추고(마지막 프레임 유지) 타이머 정밀도 요청도 풀어 둔다.</summary>
    public bool Paused
    {
        get => !_active.IsSet;
        set
        {
            if (value) _active.Reset();
            else _active.Set();
        }
    }

    private void Run()
    {
        Native.timeBeginPeriod(1);
        try
        {
            using var capturer = new ScreenCapturer();
            var clock = Stopwatch.StartNew();
            double next = 0;
            bool failing = false;

            while (_running)
            {
                if (!_active.IsSet)
                {
                    Native.timeEndPeriod(1);
                    _active.Wait();
                    Native.timeBeginPeriod(1);
                    continue;
                }

                var p = Params;
                double now = clock.Elapsed.TotalMilliseconds;
                if (now < next)
                {
                    Thread.Sleep(Math.Max(1, (int)(next - now)));
                    continue;
                }

                double period = 1000.0 / p.Fps;
                next += period;
                if (next <= now) next = now + period; // 밀렸으면 몰아서 따라잡지 않는다

                try
                {
                    Grab(capturer, p);
                    failing = false;
                }
                catch (Exception ex)
                {
                    if (!failing) ErrorLog.Write(ex); // 같은 오류가 이어지면 처음 한 번만 남긴다
                    failing = true;
                    Thread.Sleep(250);
                }
            }
        }
        finally
        {
            Native.timeEndPeriod(1);
        }
    }

    private void Grab(ScreenCapturer capturer, CaptureParams p)
    {
        var r = p.Rect;
        if (p.FollowMouse)
        {
            // UI 가 창을 옮기기를 기다리지 않고 커서 위치를 바로 쓴다.
            var (cx, cy) = Native.CursorPos();
            r = r with { X = cx - r.W / 2, Y = cy - r.H / 2 };
        }

        var pixels = capturer.Capture(r, p.Invert);
        lock (_gate)
        {
            if (_frame.Length != pixels.Length) _frame = new byte[pixels.Length];
            pixels.CopyTo(_frame);
            _frameWidth = capturer.Width;
            _frameHeight = capturer.Height;
            _frameReady = true;
        }

        if (Interlocked.Exchange(ref _presentQueued, 1) == 0)
            _dispatcher.BeginInvoke((Action)Present, DispatcherPriority.Render);
    }

    private void Present()
    {
        Volatile.Write(ref _presentQueued, 0);
        lock (_gate)
        {
            if (!_frameReady) return;
            _frameReady = false;

            if (_bitmap is null || _bitmap.PixelWidth != _frameWidth || _bitmap.PixelHeight != _frameHeight)
            {
                _bitmap = new WriteableBitmap(_frameWidth, _frameHeight, 96, 96, PixelFormats.Bgr32, null);
                _onBitmapChanged(_bitmap);
            }
            _bitmap.WritePixels(new Int32Rect(0, 0, _frameWidth, _frameHeight), _frame, _frameWidth * 4, 0);
        }
        _onPresented();
    }

    public void Dispose()
    {
        _running = false;
        _active.Set(); // 멈춰 있으면 깨워서 끝나게 한다
        _thread.Join(1000);
    }
}
