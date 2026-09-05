using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SCEWIN_Studio.Services;
using Xunit;

namespace SCEWIN_Studio.Tests;

public class DetectorTests
{
    private readonly ScewinDetector _detector = new();

    [Fact]
    public void ValidatePath_NonExistentFile_ReturnsNotFound()
    {
        var result = _detector.ValidatePath(@"C:\non_existent_folder_xyz\SCEWIN_64.exe");
        Assert.False(result.Found);
    }

    private static string? FindRealScewinPath()
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "publish", "bin", "SCEWIN", "SCEWIN_64.exe")),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "SCEWIN", "SCEWIN_64.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Скрипты и Конфиги", "SCEHUB-main", "SCEHUB-main", "SCEWIN", "5.05.01.0002", "SCEWIN_64.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    [Fact]
    public void ValidatePath_ExistingRealScewin_ReturnsDriversReady()
    {
        var realPath = FindRealScewinPath();
        if (realPath == null) return; // Skip if environment differs

        var result = _detector.ValidatePath(realPath);

        Assert.True(result.Found);
        Assert.True(result.HasAmiFldrv);
        Assert.True(result.HasAmiGendrv);
        Assert.True(result.DriversReady);
    }

    [Fact]
    public async Task AutoDetectAsync_FindsKnownScewinInstallation()
    {
        var realPath = FindRealScewinPath();
        if (realPath == null) return;

        var result = await _detector.AutoDetectAsync();

        Assert.True(result.Found);
        Assert.True(result.DriversReady);
        Assert.True(File.Exists(result.ExePath));
    }
}
