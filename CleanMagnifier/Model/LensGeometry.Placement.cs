namespace CleanMagnifier.Model;

/// <summary>모니터 위 배치 보정과 저장값 검증.</summary>
public sealed partial record LensGeometry
{
    /// <summary>뷰어가 최소한 이만큼은 모니터 안에 보여야 잡을 수 있다.</summary>
    public const double MinVisible = 40;

    /// <summary>
    /// 두 사각형이 실제 모니터 위에 있게 한다. 모니터 사이 빈 공간이나 화면 밖에 있으면 가장 가까운 모니터로 당긴다.
    /// 소스는 가로·세로 절반 이상, 뷰어는 최소 <see cref="MinVisible"/> 픽셀이 한 모니터 안에 보여야 한다.
    /// </summary>
    public LensGeometry KeepOnScreens(IReadOnlyList<RectD> monitors)
    {
        if (monitors.Count == 0) return this;
        var src = Fit(Source, monitors, Source.W / 2, Source.H / 2);
        var v = Viewer;
        var view = Fit(v, monitors, Math.Min(MinVisible, v.W), Math.Min(MinVisible, v.H));
        return this with { Source = src, ViewerX = view.X, ViewerY = view.Y };
    }

    /// <summary>
    /// 저장된 값을 믿을 수 있게 만든다. 숫자가 아니면 null(→ 기본 배치), 크기와 배율은 허용 범위로 자른다.
    /// </summary>
    public static LensGeometry? Sanitize(LensGeometry? g)
    {
        if (g is null) return null;
        double[] values = [g.Source.X, g.Source.Y, g.Source.W, g.Source.H, g.ViewerX, g.ViewerY, g.Zoom];
        if (!values.All(double.IsFinite)) return null;

        var source = g.Source with
        {
            W = Math.Clamp(g.Source.W, MinSize, MaxSourceSize),
            H = Math.Clamp(g.Source.H, MinSize, MaxSourceSize),
        };
        return (g with { Source = source }).WithZoom(g.Zoom);
    }

    /// <summary>r 이 어느 모니터와든 needW×needH 이상 겹치면 그대로, 아니면 가장 가까운 모니터 안으로 당긴다.</summary>
    private static RectD Fit(RectD r, IReadOnlyList<RectD> monitors, double needW, double needH)
    {
        foreach (var m in monitors)
        {
            double overlapW = Math.Min(r.Right, m.Right) - Math.Max(r.X, m.X);
            double overlapH = Math.Min(r.Bottom, m.Bottom) - Math.Max(r.Y, m.Y);
            if (overlapW >= needW && overlapH >= needH) return r;
        }

        double cx = r.X + r.W / 2, cy = r.Y + r.H / 2;
        var nearest = monitors.MinBy(m => DistanceSq(cx, cy, m));
        return r with
        {
            X = ClampInto(r.X, r.W, nearest.X, nearest.Right, needW),
            Y = ClampInto(r.Y, r.H, nearest.Y, nearest.Bottom, needH),
        };
    }

    private static double DistanceSq(double x, double y, RectD m)
    {
        double dx = x - Math.Clamp(x, m.X, m.Right);
        double dy = y - Math.Clamp(y, m.Y, m.Bottom);
        return dx * dx + dy * dy;
    }

    /// <summary>길이 len 인 구간이 [lo, hi] 와 최소 keep 만큼 겹치도록 시작점을 자른다.</summary>
    private static double ClampInto(double pos, double len, double lo, double hi, double keep) =>
        Math.Clamp(pos, lo - len + keep, hi - keep);
}
