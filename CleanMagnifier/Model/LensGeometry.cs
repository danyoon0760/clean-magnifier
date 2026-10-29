using System.Text.Json.Serialization;

namespace CleanMagnifier.Model;

/// <summary>
/// 돋보기의 순수 기하 상태 (단위: 물리 픽셀).
/// 뷰어 크기는 항상 <c>Source 크기 × Zoom</c> 이므로 두 사각형의 가로세로 비율은 자동으로 같다.
/// 화면 배치 보정과 저장값 검증은 LensGeometry.Placement.cs 에 있다.
/// </summary>
public sealed partial record LensGeometry(RectD Source, double ViewerX, double ViewerY, double Zoom)
{
    public const double MinZoom = 1.0;
    public const double MaxZoom = 16.0;
    public const double MinSize = 40;
    public const double MaxSourceSize = 4000;
    public const double MaxViewerSize = 8000;

    [JsonIgnore]
    public RectD Viewer => new(ViewerX, ViewerY, Source.W * Zoom, Source.H * Zoom);

    [JsonIgnore]
    public double Aspect => Source.W / Source.H;

    public RectD RectOf(LensPart part) => part == LensPart.Source ? Source : Viewer;

    /// <summary>처음 실행 시 배치: 화면 왼쪽에 16:9 소스, 그 오른쪽에 3배 뷰어.</summary>
    public static LensGeometry CreateDefault(RectD screen)
    {
        double w = Math.Round(screen.W * 0.125), h = Math.Round(w * 9 / 16);
        var source = new RectD(Math.Round(screen.X + screen.W * 0.12), Math.Round(screen.Y + screen.H * 0.3), w, h);
        return new LensGeometry(source, source.Right + 48, source.Y, 3);
    }

    /// <summary>드래그 시작 상태에서 커서가 (dx, dy) 만큼 움직였을 때의 새 상태.</summary>
    /// <param name="viewerLimit">뷰어 최대 크기(보통 가장 큰 모니터). 없으면 <see cref="MaxViewerSize"/>.</param>
    public LensGeometry Drag(LensPart part, Grip grip, double dx, double dy, SizeD? viewerLimit = null)
    {
        if (grip == Grip.Move) return Move(part, dx, dy);
        if (grip.IsCorner()) return ScaleFromCorner(part, grip, dx, dy, viewerLimit);
        if (grip.IsEdge()) return ResizeEdge(part, grip, dx, dy, viewerLimit);
        return this;
    }

    public LensGeometry Move(LensPart part, double dx, double dy) => part == LensPart.Source
        ? this with { Source = Source with { X = Source.X + dx, Y = Source.Y + dy } }
        : this with { ViewerX = ViewerX + dx, ViewerY = ViewerY + dy };

    /// <summary>
    /// 변 드래그: 그 사각형의 가로 또는 세로만 바뀌고(비율 변경), 배율은 그대로.
    /// 다른 사각형도 같은 쪽 변이 고정된 채로 같은 비율이 되도록 따라온다.
    /// </summary>
    public LensGeometry ResizeEdge(LensPart part, Grip edge, double dx, double dy, SizeD? viewerLimit = null)
    {
        bool horizontal = edge is Grip.Left or Grip.Right;
        bool lowSide = edge is Grip.Left or Grip.Top;
        double delta = horizontal ? dx : dy;

        // 계산은 소스 단위로 통일한다.
        if (part == LensPart.Viewer) delta /= Zoom;
        var max = ViewerMax(viewerLimit);
        double maxSrc = MaxSourceLength(horizontal ? max.W : max.H);

        double oldLen = horizontal ? Source.W : Source.H;
        double newLen = Math.Clamp(lowSide ? oldLen - delta : oldLen + delta, MinSize, Math.Max(MinSize, maxSrc));
        double grow = newLen - oldLen;
        double viewerGrow = grow * Zoom;

        var src = Source;
        double vx = ViewerX, vy = ViewerY;
        if (horizontal)
        {
            src = src with { W = newLen, X = lowSide ? src.X - grow : src.X };
            if (lowSide) vx -= viewerGrow;
        }
        else
        {
            src = src with { H = newLen, Y = lowSide ? src.Y - grow : src.Y };
            if (lowSide) vy -= viewerGrow;
        }
        return this with { Source = src, ViewerX = vx, ViewerY = vy };
    }

    /// <summary>
    /// 꼭짓점 드래그: 비율은 고정하고 그 사각형의 크기만 바꾼다. 다른 사각형은 그대로이므로 배율이 바뀐다.
    /// 반대쪽 꼭짓점이 고정점.
    /// </summary>
    public LensGeometry ScaleFromCorner(LensPart part, Grip corner, double dx, double dy, SizeD? viewerLimit = null)
    {
        var r = RectOf(part);
        double sx = corner.HasFlag(Grip.Right) ? 1 : -1;
        double sy = corner.HasFlag(Grip.Bottom) ? 1 : -1;

        // 커서 이동을 대각선 방향으로 투영해서 균일한 배율 s를 구한다.
        double wantW = r.W + sx * dx;
        double wantH = r.H + sy * dy;
        double s = (wantW * r.W + wantH * r.H) / (r.W * r.W + r.H * r.H);

        var viewer = Viewer;
        double sMin, sMax;
        if (part == LensPart.Source)
        {
            sMin = Max(MinSize / r.W, MinSize / r.H, viewer.W / (r.W * MaxZoom));
            sMax = Min(MaxSourceSize / r.W, MaxSourceSize / r.H, viewer.W / (r.W * MinZoom));
        }
        else
        {
            var max = ViewerMax(viewerLimit);
            sMin = Source.W * MinZoom / r.W;
            sMax = Min(Source.W * MaxZoom / r.W, max.W / r.W, max.H / r.H);
        }
        s = sMin <= sMax ? Math.Clamp(s, sMin, sMax) : 1;

        double w = r.W * s, h = r.H * s;
        double x = sx < 0 ? r.Right - w : r.X;
        double y = sy < 0 ? r.Bottom - h : r.Y;

        return part == LensPart.Source
            ? this with { Source = new RectD(x, y, w, h), Zoom = viewer.W / w }
            : this with { ViewerX = x, ViewerY = y, Zoom = w / Source.W };
    }

    /// <summary>배율 지정: 소스는 그대로, 뷰어가 왼쪽 위를 기준으로 커지거나 작아진다.</summary>
    public LensGeometry WithZoom(double zoom, SizeD? viewerLimit = null)
    {
        var m = ViewerMax(viewerLimit);
        double max = Min(MaxZoom, m.W / Source.W, m.H / Source.H);
        return this with { Zoom = Math.Clamp(zoom, MinZoom, Math.Max(MinZoom, max)) };
    }

    /// <summary>가로세로 비율 지정: 소스 가로를 유지하고 세로를 맞춘다. 배율은 유지.</summary>
    public LensGeometry WithAspect(double aspect, SizeD? viewerLimit = null)
    {
        var m = ViewerMax(viewerLimit);
        double w = Source.W, h = w / aspect;
        if (h < MinSize) { h = MinSize; w = h * aspect; }
        double maxW = MaxSourceLength(m.W), maxH = MaxSourceLength(m.H);
        if (w > maxW) { w = maxW; h = w / aspect; }
        if (h > maxH) { h = maxH; w = h * aspect; }
        return this with { Source = Source with { W = w, H = h } };
    }

    /// <summary>
    /// 뷰어가 <paramref name="viewerLimit"/> 보다 크면 배율을 낮춘다. 배율 1 에서도 넘치면 소스를 비율대로 줄인다.
    /// 모니터보다 큰 투명 창은 매 프레임 그리는 비용이 커서 렌더링이 끊기거나 실패할 수 있다.
    /// </summary>
    public LensGeometry FitViewer(SizeD viewerLimit)
    {
        var m = ViewerMax(viewerLimit);
        var src = Source;
        double shrink = Min(1, m.W / (src.W * MinZoom), m.H / (src.H * MinZoom));
        if (shrink < 1)
            src = src with { W = Math.Max(MinSize, src.W * shrink), H = Math.Max(MinSize, src.H * shrink) };
        return (this with { Source = src }).WithZoom(Zoom, viewerLimit);
    }

    public LensGeometry WithSourceCenteredAt(double x, double y) =>
        this with { Source = Source with { X = x - Source.W / 2, Y = y - Source.H / 2 } };

    /// <summary>뷰어 한 변이 <paramref name="viewerMax"/> 를 넘지 않는 현재 배율에서의 소스 한 변 최대 길이.</summary>
    private double MaxSourceLength(double viewerMax) => Math.Min(MaxSourceSize, viewerMax / Zoom);

    /// <summary>요청한 한도와 절대 한도(<see cref="MaxViewerSize"/>) 중 작은 쪽.</summary>
    private static SizeD ViewerMax(SizeD? limit) => new(
        Math.Min(MaxViewerSize, limit?.W ?? MaxViewerSize),
        Math.Min(MaxViewerSize, limit?.H ?? MaxViewerSize));

    private static double Min(double a, double b, double c) => Math.Min(a, Math.Min(b, c));
    private static double Max(double a, double b, double c) => Math.Max(a, Math.Max(b, c));
}
