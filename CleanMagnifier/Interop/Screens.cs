using CleanMagnifier.Model;
using static CleanMagnifier.Interop.Native;

namespace CleanMagnifier.Interop;

/// <summary>모니터 영역 (물리 픽셀, PerMonitorV2 기준).</summary>
internal static class Screens
{
    public static RectD Primary => new(0, 0, GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN));

    /// <summary>연결된 모니터 각각의 영역. 모니터 사이 빈 공간은 포함하지 않는다.</summary>
    public static IReadOnlyList<RectD> Monitors
    {
        get
        {
            var list = new List<RectD>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr _, IntPtr _, ref RECT r, IntPtr _) =>
            {
                list.Add(new RectD(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top));
                return true;
            }, IntPtr.Zero);
            return list.Count > 0 ? list : [Primary];
        }
    }

    /// <summary>모니터들 중 가장 큰 가로와 가장 큰 세로.</summary>
    public static SizeD Largest(IReadOnlyList<RectD> monitors) =>
        new(monitors.Max(m => m.W), monitors.Max(m => m.H));
}
