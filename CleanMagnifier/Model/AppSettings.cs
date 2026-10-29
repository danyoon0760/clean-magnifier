namespace CleanMagnifier.Model;

/// <summary>사용자 설정과 마지막 위치. 저장/불러오기는 Infrastructure.SettingsStore 가 맡는다.</summary>
public sealed class AppSettings
{
    public static readonly int[] AllowedFps = [30, 60];

    public LensGeometry? Lens { get; set; }
    public bool Smooth { get; set; }
    public int Fps { get; set; } = 60;
    public bool ShowSourceBorder { get; set; } = true;
    public bool Crosshair { get; set; }
    public bool Invert { get; set; }
    public bool FollowMouse { get; set; }
    public bool Topmost { get; set; } = true;
    public string Accent { get; set; } = AccentColors.Blue;

    /// <summary>허용 범위를 벗어난 값을 기본값으로 바로잡는다.</summary>
    public AppSettings Normalize()
    {
        if (!AllowedFps.Contains(Fps)) Fps = 60;
        if (!AccentColors.All.Any(a => a.Hex == Accent)) Accent = AccentColors.Blue;
        Lens = LensGeometry.Sanitize(Lens);
        return this;
    }
}
