namespace CleanMagnifier.Infrastructure;

/// <summary>
/// 한 사용자 세션에 하나만 실행되게 한다. 두 번째 실행은 기존 인스턴스에 "앞으로 가져오기"를 알리고 끝난다.
/// 배포 스크립트는 Exit 이벤트로 기존 인스턴스를 정상 종료시킨다(설정 저장 포함).
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string Prefix = @"Local\CleanMagnifier.";
    public const string ExitEventName = Prefix + "Exit";
    private const string ShowEventName = Prefix + "Show";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _show = new(false, EventResetMode.AutoReset, ShowEventName);
    private readonly EventWaitHandle _exit = new(false, EventResetMode.AutoReset, ExitEventName);
    private readonly ManualResetEvent _stop = new(false);
    private readonly Thread _listener;

    /// <summary>두 이벤트는 리스너 스레드에서 발생한다.</summary>
    public event Action? ShowRequested;
    public event Action? ExitRequested;

    private SingleInstance(Mutex mutex)
    {
        _mutex = mutex;
        _listener = new Thread(Listen) { IsBackground = true, Name = "CleanMagnifier.SingleInstance" };
        _listener.Start();
    }

    /// <summary>첫 인스턴스면 소유권을 얻고, 이미 실행 중이면 null.</summary>
    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(true, Prefix + "Instance", out bool createdNew);
        if (createdNew) return new SingleInstance(mutex);
        mutex.Dispose();
        return null;
    }

    public static void SignalExistingToShow()
    {
        if (EventWaitHandle.TryOpenExisting(ShowEventName, out var ev))
        {
            using (ev) ev.Set();
        }
    }

    private void Listen()
    {
        WaitHandle[] handles = [_show, _exit, _stop];
        while (true)
        {
            switch (WaitHandle.WaitAny(handles))
            {
                case 0: ShowRequested?.Invoke(); break;
                case 1: ExitRequested?.Invoke(); break;
                default: return;
            }
        }
    }

    /// <summary>뮤텍스를 얻은 스레드(UI)에서 호출해야 한다.</summary>
    public void Dispose()
    {
        _stop.Set();
        _listener.Join(500);
        _mutex.ReleaseMutex();
        _mutex.Dispose();
        _show.Dispose();
        _exit.Dispose();
        _stop.Dispose();
    }
}
