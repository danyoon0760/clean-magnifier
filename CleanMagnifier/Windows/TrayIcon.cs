using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Forms = System.Windows.Forms;

namespace CleanMagnifier.Windows;

/// <summary>
/// 알림 영역 아이콘. 두 창은 작업표시줄·Alt+Tab 에 없으므로, 가려지거나 잃어버렸을 때 되찾는 통로다.
/// 왼쪽 클릭: 앞으로 가져오기. 오른쪽 클릭: 뷰어와 같은 설정 메뉴(+ 앞으로 가져오기).
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly MagnifierController _controller;
    private readonly Forms.NotifyIcon _icon;
    private readonly ContextMenu _menu = new() { Placement = PlacementMode.MousePoint };

    public TrayIcon(MagnifierController controller)
    {
        _controller = controller;

        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))!;
        using (var stream = resource.Stream)
        {
            _icon = new Forms.NotifyIcon
            {
                Icon = new System.Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize),
                Text = "깔끔한 돋보기",
                Visible = true,
            };
        }
        _icon.MouseUp += OnMouseUp;
    }

    private void OnMouseUp(object? sender, Forms.MouseEventArgs e)
    {
        // 메뉴가 바깥 클릭으로 닫히려면 우리 창이 활성 상태여야 하므로 먼저 앞으로 가져온다.
        _controller.BringToFront();
        if (e.Button != Forms.MouseButtons.Right) return;

        ViewerMenu.Populate(_menu, _controller, fromTray: true);
        _menu.IsOpen = true;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Icon?.Dispose();
        _icon.Dispose();
    }
}
