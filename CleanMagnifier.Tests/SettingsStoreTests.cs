using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using CleanMagnifier.Infrastructure;
using CleanMagnifier.Model;

namespace CleanMagnifier.Tests;

public class SettingsStoreTests
{
    [Fact]
    public void FromJson_FixesOutOfRangeValues()
    {
        var s = SettingsStore.FromJson("""{ "Fps": 0, "Accent": "not-a-color", "Lens": { "Source": { "X": 0, "Y": 0, "W": 0, "H": 0 }, "ViewerX": 0, "ViewerY": 0, "Zoom": 0 } }""");

        Assert.Equal(60, s.Fps);
        Assert.Equal(AccentColors.Blue, s.Accent);
        Assert.NotNull(s.Lens);
        Assert.Equal(LensGeometry.MinZoom, s.Lens!.Zoom, 6);
        Assert.Equal(LensGeometry.MinSize, s.Lens.Source.W, 6);
    }

    [Fact]
    public void FromJson_CorruptJsonFallsBackToDefaults()
    {
        var s = SettingsStore.FromJson("{ this is not json");

        Assert.Null(s.Lens);
        Assert.Equal(60, s.Fps);
        Assert.True(s.Topmost);
    }

    [Fact]
    public void Load_UnreadablePathFallsBackToDefaults()
    {
        // 파일은 있지만 읽기 권한이 없으면 IOException 이 아닌 UnauthorizedAccessException 이 난다.
        var path = Path.Combine(Path.GetTempPath(), $"CleanMagnifierTests-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{ "Fps": 30 }""");
        var file = new FileInfo(path);
        var deny = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ReadData, AccessControlType.Deny);
        var acl = file.GetAccessControl();
        acl.AddAccessRule(deny);
        file.SetAccessControl(acl);
        try
        {
            var errors = new List<Exception>();
            var s = SettingsStore.Load(path, errors.Add);

            Assert.IsType<UnauthorizedAccessException>(Assert.Single(errors));
            Assert.Null(s.Lens);
            Assert.Equal(60, s.Fps);
        }
        finally
        {
            acl.RemoveAccessRule(deny);
            file.SetAccessControl(acl);
            file.Delete();
        }
    }

    [Fact]
    public void FromJson_KeepsValidValues()
    {
        var s = SettingsStore.FromJson("""{ "Fps": 30, "Accent": "#22C55E", "Crosshair": true }""");

        Assert.Equal(30, s.Fps);
        Assert.Equal(AccentColors.Green, s.Accent);
        Assert.True(s.Crosshair);
    }
}
