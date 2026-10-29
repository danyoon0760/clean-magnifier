using System.Windows;

namespace CleanMagnifier.Controls;

/// <summary>
/// 창 안에서 '보이는 사각형'을 둘러싼 여백과 잡기 영역(DIP). XAML({x:Static})과 코드가 같은 값을 쓴다.
/// 창 크기 = 사각형 + 여백×2, 잡기 영역은 사각형 테두리 안팎으로 <see cref="HitBand"/> 만큼.
/// </summary>
public static class ChromeLayout
{
    /// <summary>변/꼭짓점을 잡을 수 있는 테두리 안팎 폭.</summary>
    public const double HitBand = 7;

    /// <summary>꼭짓점으로 판정하는 범위(변 방향 길이).</summary>
    public const double CornerZone = 16;

    /// <summary>소스 창 여백: 캡처 영역 바깥에 테두리를 그려 내용을 가리지 않는다.</summary>
    public const double SourceMargin = 8;

    /// <summary>뷰어 창 여백: 그림자가 그려지는 자리.</summary>
    public const double ViewerMargin = 14;

    public const double ViewerCornerRadius = 10;

    // XAML 에서 쓰는 Thickness
    public static readonly Thickness SourceHitArea = new(SourceMargin - HitBand);
    public static readonly Thickness SourceHalo = new(SourceMargin - 3);  // 1px 어두운 외곽선
    public static readonly Thickness SourceFrame = new(SourceMargin - 2); // 2px 강조색 테두리
    public static readonly Thickness ViewerHitArea = new(ViewerMargin - HitBand);
    public static readonly Thickness ViewerContent = new(ViewerMargin);
    public static readonly CornerRadius ViewerCorner = new(ViewerCornerRadius);
    public static readonly CornerRadius ViewerHitCorner = new(ViewerCornerRadius + HitBand);
}
