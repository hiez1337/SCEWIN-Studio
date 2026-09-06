using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ModernWpf.Controls;
using SCEWIN_Studio.Services;
using SCEWIN_Studio.ViewModels;
using Xunit;

namespace SCEWIN_Studio.Tests;

public class StartupAndProcessTests
{
    [Fact]
    public void KillOrphanProcesses_DoesNotThrow()
    {
        // Must execute cleanly even when no SCEWIN processes are running
        var ex = Record.Exception(() => ScewinRunner.KillOrphanProcesses());
        Assert.Null(ex);

        var runner = new ScewinRunner();
        var exRunner = Record.Exception(() => runner.KillOrphans());
        Assert.Null(exRunner);
    }

    [Fact]
    public void KillOrphanProcesses_TargetsAllVariants()
    {
        Assert.Contains("SCEWIN_64", ScewinRunner.TargetProcessNames);
        Assert.Contains("SCEWIN64", ScewinRunner.TargetProcessNames);
        Assert.Contains("SCEWIN", ScewinRunner.TargetProcessNames);
    }

    [Fact]
    public void LogCrash_WritesCrashLogFile_WithFullDetails()
    {
        var logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, App.CrashLogFileName);
        if (File.Exists(logFile))
        {
            try { File.Delete(logFile); } catch { }
        }

        var innerEx = new InvalidOperationException("Inner failure detail");
        var outerEx = new ApplicationException("Outer startup failure", innerEx);

        App.LogCrash("TestContext_OnStartup", outerEx);

        Assert.True(File.Exists(logFile), "crash_startup.log should have been created.");
        var content = File.ReadAllText(logFile);

        Assert.Contains("TestContext_OnStartup", content);
        Assert.Contains("Outer startup failure", content);
        Assert.Contains("Inner failure detail", content);
        Assert.Contains("ApplicationException", content);
        Assert.Contains("InvalidOperationException", content);
        Assert.Contains("OS Version", content);
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SCEWIN_Studio.sln")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return AppDomain.CurrentDomain.BaseDirectory;
    }

    [Fact]
    public void AllXamlFiles_DoNotContainInvalidIconAttributes()
    {
        var solutionDir = FindSolutionRoot();
        var srcDir = Path.Combine(solutionDir, "src", "SCEWIN_Studio");

        var xamlFiles = Directory.GetFiles(srcDir, "*.xaml", SearchOption.AllDirectories);
        Assert.NotEmpty(xamlFiles);

        foreach (var xamlPath in xamlFiles)
        {
            var xamlText = File.ReadAllText(xamlPath);

            Assert.DoesNotContain("Icon=\"SpeedHigh\"", xamlText);
            Assert.DoesNotContain("Icon=\"Cpu\"", xamlText);

            var iconMatches = Regex.Matches(xamlText, @"Icon=""(?<name>[A-Za-z0-9_]+)""");
            foreach (Match match in iconMatches)
            {
                var iconName = match.Groups["name"].Value;
                bool isSymbol = Enum.TryParse<Symbol>(iconName, out _);
                Assert.True(isSymbol, $"Icon attribute '{iconName}' in {Path.GetFileName(xamlPath)} is not a valid ModernWpf Symbol!");
            }
        }
    }

    [Fact]
    public void MainViewModel_SetLanguage_PersistsAndUpdatesL10n()
    {
        var settingsService = new SettingsService();
        var l10n = new LocalizationService();
        var vm = new MainViewModel(
            new ScewinParser(),
            new ScewinDetector(),
            new ScewinRunner(),
            settingsService,
            new ScewinDownloader(),
            l10n);

        vm.SetLanguage("en-US");
        Assert.Equal("en-US", l10n.CurrentLanguage);
        Assert.Equal("en-US", settingsService.Settings.Language);

        vm.SetLanguage("ru-RU");
        Assert.Equal("ru-RU", l10n.CurrentLanguage);
        Assert.Equal("ru-RU", settingsService.Settings.Language);
    }

    [Fact]
    public void MainViewModel_RestartAsAdminCommand_Exists()
    {
        var vm = new MainViewModel();
        Assert.NotNull(vm.RestartAsAdminCommand);
        Assert.True(vm.RestartAsAdminCommand.CanExecute(null));
    }
}
