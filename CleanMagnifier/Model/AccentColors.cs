namespace CleanMagnifier.Model;

/// <summary>테두리(강조) 색 선택지. 설정에는 이 중 하나의 Hex 만 저장된다.</summary>
public static class AccentColors
{
    public const string Blue = "#3B82F6";
    public const string Green = "#22C55E";
    public const string Orange = "#F59E0B";
    public const string White = "#F5F5F5";

    public static readonly (string Name, string Hex)[] All =
    [
        ("파랑", Blue),
        ("초록", Green),
        ("주황", Orange),
        ("흰색", White),
    ];
}
