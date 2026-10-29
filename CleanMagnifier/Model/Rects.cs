using System.Text.Json.Serialization;

namespace CleanMagnifier.Model;

/// <summary>물리 픽셀 단위 사각형 (계산용 실수).</summary>
public readonly record struct RectD(double X, double Y, double W, double H)
{
    [JsonIgnore]
    public double Right => X + W;

    [JsonIgnore]
    public double Bottom => Y + H;

    public PixelRect ToPixels() =>
        new((int)Math.Round(X), (int)Math.Round(Y), (int)Math.Round(W), (int)Math.Round(H));
}

/// <summary>물리 픽셀 단위 크기.</summary>
public readonly record struct SizeD(double W, double H);

/// <summary>창 배치와 캡처에 쓰는 정수 픽셀 사각형.</summary>
public readonly record struct PixelRect(int X, int Y, int W, int H);
