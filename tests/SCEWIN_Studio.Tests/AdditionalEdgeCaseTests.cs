using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SCEWIN_Studio.Models;
using SCEWIN_Studio.Services;
using SCEWIN_Studio.ViewModels;
using Xunit;

namespace SCEWIN_Studio.Tests;

public class AdditionalEdgeCaseTests
{
    private readonly ScewinParser _parser = new();
    private readonly ScewinDownloader _downloader = new();
    private readonly SettingsService _settingsService = new();

    [Fact]
    public void Parse_NumericTokenWithoutOptions_TracksModifications()
    {
        const string rawNumeric = @"HIICrc32= 12345678

Setup Question	= PPT Limit
Help String	= Package Power Tracking limit in mW
Token	=A1
Offset	=20
Width	=04
Value	=142000
";

        var dump = _parser.Parse(rawNumeric);
        Assert.Single(dump.Tokens);

        var token = dump.Tokens[0];
        Assert.False(token.HasOptions);
        Assert.Equal("142000", token.OriginalNumericValue);
        Assert.Equal("142000", token.CurrentDisplayValue);
        Assert.False(token.IsModified);

        // Edit numeric value
        token.CustomNumericValue = "150000";
        Assert.True(token.IsModified);
        Assert.Equal("150000", token.CurrentDisplayValue);

        // Revert to original
        token.CustomNumericValue = "142000";
        Assert.False(token.IsModified);

        // Generate diff
        token.CustomNumericValue = "180000";
        var diff = _parser.GenerateDiffScript(dump, new[] { token });
        Assert.Contains("Value\t=180000", diff);
        Assert.Contains("Token\t=A1", diff);
    }

    [Fact]
    public void Parse_NumericTokenWithAngleBrackets_PreservesAngleBracketsInDiff()
    {
        const string rawWithBrackets = @"HIICrc32= 8F23D09B

Setup Question	= Adjust V1.8
Help String	= Adjust voltage
Token	=42
Offset	=08
Width	=01
Value	=<128>
";

        var dump = _parser.Parse(rawWithBrackets);
        Assert.Single(dump.Tokens);

        var token = dump.Tokens[0];
        Assert.True(token.NumericHasAngleBrackets);
        Assert.Equal("128", token.OriginalNumericValue);
        Assert.Equal("128", token.CurrentDisplayValue);
        Assert.False(token.IsModified);

        // User edits value to 256 without angle brackets
        token.CustomNumericValue = "256";
        Assert.True(token.IsModified);

        var diff = _parser.GenerateDiffScript(dump, new[] { token });
        Assert.Contains("Value\t=<256>", diff);
    }

    [Fact]
    public void GenerateDiff_EmptyModifiedList_ReturnsHeaderOnly()
    {
        var dump = new ScewinDump
        {
            HiiCrc32 = "AABBCCDD",
            UtilityVersion = "Ver 5.05.01.0002"
        };

        var diff = _parser.GenerateDiffScript(dump, Enumerable.Empty<ScewinToken>());
        Assert.Contains("HIICrc32= AABBCCDD", diff);
        Assert.DoesNotContain("Setup Question", diff);
        Assert.DoesNotContain("Token", diff);
    }

    [Fact]
    public async Task Downloader_ProvisionsAllRequiredFilesAndDrivers()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "scewin_test_" + Guid.NewGuid().ToString("N"));
        try
        {
            var progressUpdates = new System.Collections.Generic.List<(int pct, string msg)>();
            var progress = new Progress<(int percentage, string status)>(p => progressUpdates.Add(p));

            var result = await _downloader.DownloadOrProvisionScewinAsync(tempDir, progress);

            Assert.True(result.success);
            Assert.True(File.Exists(result.exePath));
            Assert.True(File.Exists(Path.Combine(tempDir, "amifldrv64.sys")));
            Assert.True(File.Exists(Path.Combine(tempDir, "amigendrv64.sys")));
            Assert.True(new FileInfo(result.exePath).Length > 100_000);
            Assert.True(new FileInfo(Path.Combine(tempDir, "amifldrv64.sys")).Length > 10_000);
            Assert.True(new FileInfo(Path.Combine(tempDir, "amigendrv64.sys")).Length > 10_000);
            Assert.NotEmpty(progressUpdates);
            Assert.Contains(progressUpdates, p => p.pct == 100);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void Detector_ValidatePath_DetectsMissingDriversCorrectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "det_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var exePath = Path.Combine(tempDir, "SCEWIN_64.exe");
            File.WriteAllText(exePath, "fake exe");

            var detector = new ScewinDetector();
            var res = detector.ValidatePath(exePath);

            Assert.True(res.Found);
            Assert.False(res.HasAmiFldrv);
            Assert.False(res.HasAmiGendrv);
            Assert.False(res.DriversReady);

            // Now create one driver
            File.WriteAllText(Path.Combine(tempDir, "amifldrv64.sys"), "fake sys");
            res = detector.ValidatePath(exePath);
            Assert.True(res.HasAmiFldrv);
            Assert.False(res.HasAmiGendrv);
            Assert.False(res.DriversReady);

            // Now create both drivers
            File.WriteAllText(Path.Combine(tempDir, "amigendrv64.sys"), "fake sys");
            res = detector.ValidatePath(exePath);
            Assert.True(res.HasAmiFldrv);
            Assert.True(res.HasAmiGendrv);
            Assert.True(res.DriversReady);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void MainViewModel_IsDumpLoaded_TracksStateProperly()
    {
        var vm = new MainViewModel(
            _parser,
            new ScewinDetector(),
            new ScewinRunner(),
            _settingsService,
            _downloader,
            LocalizationService.Instance);

        // Before dump is loaded
        vm.CurrentDump = null;
        Assert.False(vm.IsDumpLoaded);

        // Load dump
        var dump = new ScewinDump { HiiCrc32 = "TEST1234" };
        vm.SetCurrentDump(dump);
        Assert.True(vm.IsDumpLoaded);
        Assert.Equal("TEST1234", vm.CurrentDump?.HiiCrc32);
    }

    [Fact]
    public void SettingsService_SaveAndLoad_RoundtripsSuccessfully()
    {
        var testPath = @"C:\CustomPath\SCEWIN_64.exe";
        _settingsService.Settings.ScewinExePath = testPath;
        _settingsService.Settings.Language = "en-US";
        _settingsService.Settings.DarkTheme = true;
        _settingsService.Save();

        // Create new instance to reload
        var reloaded = new SettingsService();
        Assert.Equal(testPath, reloaded.Settings.ScewinExePath);
        Assert.Equal("en-US", reloaded.Settings.Language);
        Assert.True(reloaded.Settings.DarkTheme);

        // Restore to ru-RU
        _settingsService.Settings.Language = "ru-RU";
        _settingsService.Save();
    }
}
