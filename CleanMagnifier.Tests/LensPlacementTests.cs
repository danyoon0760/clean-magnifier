using CleanMagnifier.Model;
using static CleanMagnifier.Tests.LensGeometryTests;

namespace CleanMagnifier.Tests;

/// <summary>모니터 위 배치 보정(KeepOnScreens)과 저장값 검증(Sanitize).</summary>
public class LensPlacementTests
{
    // 왼쪽 1920x1080, 오른쪽 1280x720 (위쪽 정렬) → 오른쪽 모니터 아래는 빈 공간
    private static readonly RectD[] TwoMonitors = [new(0, 0, 1920, 1080), new(1920, 0, 1280, 720)];

    [Fact]
    public void KeepOnScreens_LeavesVisibleRectsAlone()
    {
        var g = Sample();
        Assert.Equal(g, g.KeepOnScreens(TwoMonitors));
    }

    [Fact]
    public void KeepOnScreens_PullsBackOffscreenViewer()
    {
        var g = (Sample() with { ViewerX = 99999 }).KeepOnScreens(TwoMonitors);
        Assert.Equal(3200 - LensGeometry.MinVisible, g.ViewerX, 6);
    }

    [Fact]
    public void KeepOnScreens_MovesSourceOutOfGapBetweenMonitors()
    {
        // 오른쪽 모니터 아래 빈 공간(어느 모니터에도 없음)
        var g = (Sample() with { Source = new RectD(2400, 900, 200, 100) }).KeepOnScreens(TwoMonitors);

        Assert.Equal(2400, g.Source.X, 6);
        Assert.Equal(720 - 50, g.Source.Y, 6); // 절반 이상이 오른쪽 모니터 안
    }

    [Fact]
    public void Sanitize_ClampsBadValues()
    {
        var bad = new LensGeometry(new RectD(0, 0, 0, -5), 0, 0, 0);
        var g = LensGeometry.Sanitize(bad)!;

        Assert.Equal(LensGeometry.MinSize, g.Source.W, 6);
        Assert.Equal(LensGeometry.MinSize, g.Source.H, 6);
        Assert.Equal(LensGeometry.MinZoom, g.Zoom, 6);
    }

    [Fact]
    public void Sanitize_RejectsNonFinite()
    {
        Assert.Null(LensGeometry.Sanitize(Sample() with { Zoom = double.NaN }));
        Assert.Null(LensGeometry.Sanitize(null));
    }
}
