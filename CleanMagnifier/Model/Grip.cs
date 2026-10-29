namespace CleanMagnifier.Model;

/// <summary>두 사각형 중 어느 쪽을 조작하는지.</summary>
public enum LensPart { Source, Viewer }

/// <summary>마우스가 잡은 부위. 변/꼭짓점은 방향 비트의 조합.</summary>
[Flags]
public enum Grip
{
    None = 0,
    Left = 1,
    Top = 2,
    Right = 4,
    Bottom = 8,
    Move = 16,
    TopLeft = Top | Left,
    TopRight = Top | Right,
    BottomLeft = Bottom | Left,
    BottomRight = Bottom | Right,
}

public static class GripExtensions
{
    public static bool IsCorner(this Grip g) =>
        g is Grip.TopLeft or Grip.TopRight or Grip.BottomLeft or Grip.BottomRight;

    public static bool IsEdge(this Grip g) =>
        g is Grip.Left or Grip.Top or Grip.Right or Grip.Bottom;
}
