#:property TargetFramework=net10.0-windows
#:property UseWPF=true
#:property PublishAot=false

// 앱 아이콘 생성기: dotnet run tools/make-icon.cs -- <출력 폴더>
// 작은 사각형(소스)이 큰 사각형(뷰어)으로 확대되는 모양을 벡터로 그려 여러 크기의 PNG를 ICO 하나로 묶는다.

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

string outDir = args.Length > 0 ? args[0] : ".";
Directory.CreateDirectory(outDir);

var thread = new Thread(() =>
{
    int[] sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];
    var pngs = sizes.Select(s => (Size: s, Data: RenderPng(s))).ToList();

    File.WriteAllBytes(Path.Combine(outDir, "app.png"), pngs[^1].Data);
    using var ico = File.Create(Path.Combine(outDir, "app.ico"));
    WriteIco(ico, pngs);
});
thread.SetApartmentState(ApartmentState.STA);
thread.Start();
thread.Join();
Console.WriteLine($"아이콘 생성 완료: {Path.GetFullPath(outDir)}");

static byte[] RenderPng(int size)
{
    var visual = new DrawingVisual();
    using (var dc = visual.RenderOpen())
    {
        dc.PushTransform(new ScaleTransform(size / 256.0, size / 256.0));
        Draw(dc, small: size <= 32);
        dc.Pop();
    }

    var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
    bmp.Render(visual);

    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bmp));
    using var ms = new MemoryStream();
    encoder.Save(ms);
    return ms.ToArray();
}

// 256x256 좌표계. 작은 크기에서는 선을 굵게 해서 뭉개지지 않게 한다.
static void Draw(DrawingContext dc, bool small)
{
    var accent = Color.FromRgb(0x3B, 0x82, 0xF6);
    var accentLight = Color.FromRgb(0x93, 0xC5, 0xFD);

    // 배경: 어두운 남색 라운드 사각형
    var bg = new LinearGradientBrush(Color.FromRgb(0x2A, 0x36, 0x4F), Color.FromRgb(0x0F, 0x17, 0x2A), 90);
    dc.DrawRoundedRectangle(bg, null, new Rect(8, 8, 240, 240), 54, 54);
    dc.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)), 2),
        new Rect(9, 9, 238, 238), 53, 53);

    // 두 사각형은 같은 4:3 비율
    var src = new Rect(30, 152, 72, 54);
    var view = new Rect(92, 38, 136, 102);

    // 확대 투영선
    var beam = new Pen(new SolidColorBrush(Color.FromArgb(small ? (byte)0x00 : (byte)0x70, accentLight.R, accentLight.G, accentLight.B)), 4)
    {
        DashStyle = new DashStyle([2, 2], 0),
        DashCap = PenLineCap.Round,
    };
    dc.DrawLine(beam, src.TopLeft, view.TopLeft);
    dc.DrawLine(beam, src.BottomRight, view.BottomRight);

    // 뷰어: 밝은 바탕에 확대된 '픽셀' 몇 개
    double viewStroke = small ? 14 : 9;
    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)), null, view, 14, 14);
    DrawPixels(dc, view, accent, accentLight, inset: small ? 18 : 16);
    dc.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(accent), viewStroke), view, 14, 14);

    // 소스: 파란 테두리만
    double srcStroke = small ? 14 : 9;
    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0x40, accent.R, accent.G, accent.B)),
        new Pen(new SolidColorBrush(accent), srcStroke), src, 8, 8);
}

static void DrawPixels(DrawingContext dc, Rect r, Color dark, Color light, double inset)
{
    // 3x2 격자에 체커 무늬 — '확대된 픽셀' 느낌
    var inner = new Rect(r.X + inset, r.Y + inset, r.Width - inset * 2, r.Height - inset * 2);
    double cw = inner.Width / 3, ch = inner.Height / 2;
    var darkBrush = new SolidColorBrush(dark);
    var lightBrush = new SolidColorBrush(light);
    for (int y = 0; y < 2; y++)
        for (int x = 0; x < 3; x++)
        {
            var brush = (x + y) % 2 == 0 ? darkBrush : lightBrush;
            dc.DrawRoundedRectangle(brush, null,
                new Rect(inner.X + x * cw + 2, inner.Y + y * ch + 2, cw - 4, ch - 4), 4, 4);
        }
}

static void WriteIco(Stream s, List<(int Size, byte[] Data)> images)
{
    using var w = new BinaryWriter(s);
    w.Write((short)0);           // reserved
    w.Write((short)1);           // type: icon
    w.Write((short)images.Count);

    int offset = 6 + 16 * images.Count;
    foreach (var (size, data) in images)
    {
        w.Write((byte)(size >= 256 ? 0 : size));
        w.Write((byte)(size >= 256 ? 0 : size));
        w.Write((byte)0);        // palette
        w.Write((byte)0);        // reserved
        w.Write((short)1);       // planes
        w.Write((short)32);      // bpp
        w.Write(data.Length);
        w.Write(offset);
        offset += data.Length;
    }
    foreach (var (_, data) in images) w.Write(data);
}
