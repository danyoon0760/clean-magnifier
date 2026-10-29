using System.IO;
using System.Text.Json;
using CleanMagnifier.Model;

namespace CleanMagnifier.Infrastructure;

/// <summary>설정을 %AppData%\CleanMagnifier\settings.json 에 읽고 쓴다. 실패해도 앱은 계속 돈다.</summary>
internal static class SettingsStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static AppSettings Load() => Load(AppPaths.Settings, ErrorLog.Write);

    internal static AppSettings Load(string path, Action<Exception> logError)
    {
        try
        {
            if (File.Exists(path)) return FromJson(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            logError(ex); // 읽을 수 없으면(잠김·권한 등) 기본값으로 시작한다.
        }
        return new();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Folder);
            File.WriteAllText(AppPaths.Settings, JsonSerializer.Serialize(settings, Json));
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex); // 저장 실패는 치명적이지 않다.
        }
    }

    /// <summary>JSON 을 읽고 값을 바로잡는다. 손상된 JSON 이면 전부 기본값.</summary>
    internal static AppSettings FromJson(string json)
    {
        AppSettings? s;
        try
        {
            s = JsonSerializer.Deserialize<AppSettings>(json, Json);
        }
        catch (JsonException)
        {
            s = null;
        }
        return (s ?? new()).Normalize();
    }
}
