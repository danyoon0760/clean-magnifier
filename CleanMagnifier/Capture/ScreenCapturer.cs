using System.ComponentModel;
using System.Runtime.InteropServices;
using CleanMagnifier.Model;
using static CleanMagnifier.Interop.Native;

namespace CleanMagnifier.Capture;

/// <summary>
/// 화면의 한 영역을 GDI BitBlt 로 DIB 버퍼에 복사한다. 캡처 스레드 하나에서만 쓴다.
/// 우리 창들은 WDA_EXCLUDEFROMCAPTURE 로 제외되므로 그 뒤의 화면이 찍힌다.
/// CAPTUREBLT 는 일부 환경에서 마우스 커서를 깜빡이게 하므로 쓰지 않는다 (DWM 환경에선 없어도 레이어드 창이 찍힌다).
/// </summary>
internal sealed class ScreenCapturer : IDisposable
{
    private IntPtr _memDc, _dib, _oldBitmap, _bits;

    public int Width { get; private set; }
    public int Height { get; private set; }

    /// <summary>영역을 캡처해 BGRX 픽셀(top-down)을 돌려준다. 다음 호출 전까지만 유효하다.</summary>
    public unsafe ReadOnlySpan<byte> Capture(PixelRect r, bool invert)
    {
        int width = Math.Max(1, r.W), height = Math.Max(1, r.H);
        if (_memDc == IntPtr.Zero || width != Width || height != Height) Recreate(width, height);

        IntPtr screen = GetDC(IntPtr.Zero);
        bool ok = BitBlt(_memDc, 0, 0, width, height, screen, r.X, r.Y, SRCCOPY);
        int error = Marshal.GetLastPInvokeError();
        ReleaseDC(IntPtr.Zero, screen);
        // 잠금 화면·UAC 같은 보안 데스크톱에선 실패한다. 예외로 알리면 캡처 루프가 한 번만 기록하고 쉬엄쉬엄 재시도한다.
        if (!ok) throw new Win32Exception(error, "화면을 캡처하지 못했습니다.");

        int count = width * height;
        if (invert)
        {
            var px = new Span<uint>((void*)_bits, count);
            for (int i = 0; i < px.Length; i++) px[i] ^= 0x00FFFFFF;
        }
        return new ReadOnlySpan<byte>((void*)_bits, count * 4);
    }

    private void Recreate(int width, int height)
    {
        ReleaseGdi();

        var header = new BITMAPINFOHEADER
        {
            biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = width,
            biHeight = -height, // top-down
            biPlanes = 1,
            biBitCount = 32,
        };

        IntPtr screen = GetDC(IntPtr.Zero);
        _memDc = CreateCompatibleDC(screen);
        ReleaseDC(IntPtr.Zero, screen);
        _dib = CreateDIBSection(_memDc, ref header, 0, out _bits, IntPtr.Zero, 0);
        if (_dib == IntPtr.Zero || _bits == IntPtr.Zero)
        {
            ReleaseGdi();
            throw new InvalidOperationException($"캡처 버퍼({width}x{height})를 만들지 못했습니다.");
        }
        _oldBitmap = SelectObject(_memDc, _dib);

        Width = width;
        Height = height;
    }

    private void ReleaseGdi()
    {
        if (_memDc == IntPtr.Zero) return;
        if (_oldBitmap != IntPtr.Zero) SelectObject(_memDc, _oldBitmap);
        if (_dib != IntPtr.Zero) DeleteObject(_dib);
        DeleteDC(_memDc);
        _memDc = _dib = _oldBitmap = _bits = IntPtr.Zero;
        Width = Height = 0;
    }

    public void Dispose() => ReleaseGdi();
}
