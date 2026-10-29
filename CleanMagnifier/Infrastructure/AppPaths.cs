using System.IO;

namespace CleanMagnifier.Infrastructure;

/// <summary>%AppData%\CleanMagnifier 아래 파일 경로.</summary>
public static class AppPaths
{
    public static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CleanMagnifier");

    public static readonly string Settings = Path.Combine(Folder, "settings.json");

    public static readonly string ErrorLog = Path.Combine(Folder, "error.log");
}
