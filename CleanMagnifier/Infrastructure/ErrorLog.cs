using System.IO;

namespace CleanMagnifier.Infrastructure;

/// <summary>예외를 %AppData%\CleanMagnifier\error.log 에 남긴다. 기록 실패는 무시한다.</summary>
internal static class ErrorLog
{
    private static readonly object Gate = new();

    public static void Write(Exception? ex)
    {
        if (ex is null) return;
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.Folder);
                File.AppendAllText(AppPaths.ErrorLog, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch (Exception)
        {
            // 로그를 못 남겨도 앱 동작에는 영향이 없다.
        }
    }
}
