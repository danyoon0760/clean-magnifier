using System.Windows;
using System.Windows.Threading;
using CleanMagnifier.Infrastructure;
using CleanMagnifier.Themes;

namespace CleanMagnifier;

public partial class App : Application
{
    private SingleInstance? _instance;
    private MagnifierController? _controller;
    private bool _crashing;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, ev) => ErrorLog.Write(ev.ExceptionObject as Exception);

        // 이미 실행 중이면 그쪽을 앞으로 불러내고 끝낸다.
        _instance = SingleInstance.TryAcquire();
        if (_instance is null)
        {
            SingleInstance.SignalExistingToShow();
            Shutdown();
            return;
        }

        Theme.Apply(this);
        Theme.Watch(this);

        var controller = new MagnifierController();
        _controller = controller;
        _instance.ShowRequested += () => Dispatcher.BeginInvoke(controller.BringToFront);
        _instance.ExitRequested += () => Dispatcher.BeginInvoke(controller.Exit);
        controller.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        base.OnExit(e);
    }

    /// <summary>처리 못 한 예외: 로그를 남기고 알린 뒤 정상 종료 절차(설정 저장·트레이 정리)를 밟는다.</summary>
    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        ErrorLog.Write(e.Exception);
        if (_crashing) return;
        _crashing = true;

        MessageBox.Show(
            $"예기치 못한 오류로 돋보기를 종료합니다.\n\n{e.Exception.Message}\n\n자세한 내용: {AppPaths.ErrorLog}",
            "깔끔한 돋보기", MessageBoxButton.OK, MessageBoxImage.Error);

        if (_controller is not null) _controller.Exit();
        else Shutdown();
    }
}
