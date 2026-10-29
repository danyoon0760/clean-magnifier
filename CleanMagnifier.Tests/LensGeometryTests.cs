using CleanMagnifier.Model;

namespace CleanMagnifier.Tests;

public class LensGeometryTests
{
    private const double Eps = 1e-6;

    // 소스 200x100 @ (100,100), 뷰어 @ (400,100), 배율 3 → 뷰어 600x300
    internal static LensGeometry Sample() => new(new RectD(100, 100, 200, 100), 400, 100, 3);

    private static void AssertSameAspect(LensGeometry g) =>
        Assert.Equal(g.Source.W / g.Source.H, g.Viewer.W / g.Viewer.H, 6);

    [Fact]
    public void SourceRightEdge_ChangesShape_KeepsZoom()
    {
        var g = Sample().Drag(LensPart.Source, Grip.Right, 50, 999);

        Assert.Equal(250, g.Source.W, 6);
        Assert.Equal(100, g.Source.H, 6);
        Assert.Equal(3, g.Zoom, 6);
        Assert.Equal(750, g.Viewer.W, 6);
        Assert.Equal(400, g.Viewer.X, 6); // 뷰어도 왼쪽 변 고정
        AssertSameAspect(g);
    }

    [Fact]
    public void ViewerLeftEdge_AnchorsRightEdgeInBoth()
    {
        var start = Sample();
        var g = start.Drag(LensPart.Viewer, Grip.Left, -60, 0); // 뷰어 60px 넓어짐 → 소스 20px

        Assert.Equal(3, g.Zoom, 6);
        Assert.Equal(660, g.Viewer.W, 6);
        Assert.Equal(start.Viewer.Right, g.Viewer.Right, 6);
        Assert.Equal(220, g.Source.W, 6);
        Assert.Equal(start.Source.Right, g.Source.Right, 6);
        AssertSameAspect(g);
    }

    [Fact]
    public void SourceTopEdge_ChangesHeightOnly()
    {
        var g = Sample().Drag(LensPart.Source, Grip.Top, 0, 30);

        Assert.Equal(70, g.Source.H, 6);
        Assert.Equal(130, g.Source.Y, 6);
        Assert.Equal(200, g.Source.W, 6);
        Assert.Equal(3, g.Zoom, 6);
        AssertSameAspect(g);
    }

    [Fact]
    public void Edge_ClampsToMinSize()
    {
        var g = Sample().Drag(LensPart.Source, Grip.Bottom, 0, -1000);
        Assert.Equal(LensGeometry.MinSize, g.Source.H, 6);
    }

    [Fact]
    public void SourceCorner_KeepsAspect_ChangesZoom_ViewerUntouched()
    {
        var start = Sample();
        var g = start.Drag(LensPart.Source, Grip.BottomRight, 200, 100); // 2배 확대

        Assert.Equal(start.Aspect, g.Aspect, 6);
        Assert.Equal(400, g.Source.W, 6);
        Assert.Equal(1.5, g.Zoom, 6);
        Assert.Equal(start.Viewer, g.Viewer);
    }

    [Fact]
    public void ViewerTopLeftCorner_AnchorsBottomRight()
    {
        var start = Sample();
        var g = start.Drag(LensPart.Viewer, Grip.TopLeft, -300, -150); // 1.5배

        Assert.Equal(start.Aspect, g.Aspect, 6);
        Assert.Equal(900, g.Viewer.W, 6);
        Assert.Equal(4.5, g.Zoom, 6);
        Assert.Equal(start.Viewer.Right, g.Viewer.Right, 6);
        Assert.Equal(start.Viewer.Bottom, g.Viewer.Bottom, 6);
        Assert.Equal(start.Source, g.Source);
    }

    [Fact]
    public void Corner_ClampsZoomRange()
    {
        var shrink = Sample().Drag(LensPart.Viewer, Grip.BottomRight, -10000, -10000);
        Assert.Equal(LensGeometry.MinZoom, shrink.Zoom, 6);

        var tinySource = Sample().Drag(LensPart.Source, Grip.BottomRight, -10000, -10000);
        Assert.True(tinySource.Zoom <= LensGeometry.MaxZoom + Eps);
        Assert.True(tinySource.Source.H >= LensGeometry.MinSize - Eps);
    }

    [Fact]
    public void WithZoom_ResizesViewerFromTopLeft()
    {
        var g = Sample().WithZoom(2);
        Assert.Equal(400, g.Viewer.W, 6);
        Assert.Equal(400, g.Viewer.X, 6);
        Assert.Equal(16, Sample().WithZoom(100).Zoom, 6);
    }

    private static readonly SizeD FullHd = new(1920, 1080);

    [Fact]
    public void WithZoom_StopsAtViewerLimit()
    {
        var g = Sample().WithZoom(100, FullHd); // 소스 200x100 → 가로 1920/200 = 9.6배가 한계
        Assert.Equal(9.6, g.Zoom, 6);
        Assert.True(g.Viewer.W <= FullHd.W + Eps && g.Viewer.H <= FullHd.H + Eps);
    }

    [Fact]
    public void Drag_KeepsViewerWithinLimit()
    {
        var corner = Sample().Drag(LensPart.Viewer, Grip.BottomRight, 10000, 10000, FullHd);
        Assert.True(corner.Viewer.W <= FullHd.W + Eps && corner.Viewer.H <= FullHd.H + Eps);

        var edge = Sample().Drag(LensPart.Viewer, Grip.Bottom, 0, 10000, FullHd);
        Assert.Equal(FullHd.H, edge.Viewer.H, 6);
        Assert.Equal(3, edge.Zoom, 6);
    }

    [Fact]
    public void FitViewer_LowersZoomFirst()
    {
        var g = (Sample() with { Zoom = 16 }).FitViewer(FullHd); // 뷰어 3200x1600 → 넘침
        Assert.Equal(9.6, g.Zoom, 6);
        Assert.Equal(Sample().Source, g.Source);
    }

    [Fact]
    public void FitViewer_ShrinksSourceWhenTooBigAtZoomOne()
    {
        var big = new LensGeometry(new RectD(0, 0, 3000, 1000), 0, 0, 1);
        var g = big.FitViewer(FullHd);

        Assert.Equal(1, g.Zoom, 6);
        Assert.Equal(1920, g.Source.W, 6);
        Assert.Equal(640, g.Source.H, 6);
    }

    [Fact]
    public void FitViewer_LeavesFittingGeometryAlone()
    {
        Assert.Equal(Sample(), Sample().FitViewer(FullHd));
    }

    [Fact]
    public void WithAspect_KeepsZoomAndWidth()
    {
        var g = Sample().WithAspect(1);
        Assert.Equal(200, g.Source.W, 6);
        Assert.Equal(200, g.Source.H, 6);
        Assert.Equal(3, g.Zoom, 6);
        AssertSameAspect(g);
    }

    [Fact]
    public void Move_OnlyMovesThatRect()
    {
        var start = Sample();
        var g = start.Drag(LensPart.Viewer, Grip.Move, 10, 20);
        Assert.Equal(start.Source, g.Source);
        Assert.Equal(410, g.ViewerX, 6);
        Assert.Equal(120, g.ViewerY, 6);
    }

    [Fact]
    public void CreateDefault_FitsOnScreenWithMatchingAspect()
    {
        var screen = new RectD(0, 0, 1920, 1080);
        var g = LensGeometry.CreateDefault(screen);

        Assert.Equal(16.0 / 9, g.Aspect, 2);
        Assert.Equal(3, g.Zoom, 6);
        Assert.True(g.Viewer.X >= g.Source.Right);
        Assert.True(g.Viewer.Right <= screen.Right);
        Assert.True(g.Viewer.Bottom <= screen.Bottom);
        AssertSameAspect(g);
    }

    [Fact]
    public void ToPixels_RoundsEachComponent()
    {
        Assert.Equal(new PixelRect(10, 20, 31, 39), new RectD(9.6, 20.4, 30.7, 39.2).ToPixels());
    }
}
