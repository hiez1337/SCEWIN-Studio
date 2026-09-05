using System;
using System.IO;
using System.Linq;
using SCEWIN_Studio.Models;
using SCEWIN_Studio.Services;
using SCEWIN_Studio.ViewModels;
using Xunit;

namespace SCEWIN_Studio.Tests;

public class MemoryTierTests
{
    private readonly ScewinParser _parser = new();

    private string GetRealDumpPath()
    {
        var testDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "nvramBEFORE.txt");
        if (!File.Exists(testDataPath))
        {
            testDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nvramBEFORE.txt");
        }
        if (!File.Exists(testDataPath))
        {
            testDataPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "src", "SCEWIN_Studio", "nvramBEFORE.txt"));
        }
        if (!File.Exists(testDataPath))
        {
            testDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Скрипты и Конфиги", "SCEHUB-main", "SCEHUB-main", "SCEWIN", "5.05.01.0002", "nvramBEFORE.txt");
        }
        return testDataPath;
    }

    [Fact]
    public void MemoryOptions_FormattedDisplayText_FormatsHexClocksToDecimal()
    {
        var opt1 = new ScewinOption { ValueHex = "10", DisplayText = "10h Clk" };
        Assert.Equal("10h Clk (16)", opt1.FormattedDisplayText);

        var opt2 = new ScewinOption { ValueHex = "12", DisplayText = "12h Clk" };
        Assert.Equal("12h Clk (18)", opt2.FormattedDisplayText);

        var opt3 = new ScewinOption { ValueHex = "26", DisplayText = "26h Clk" };
        Assert.Equal("26h Clk (38)", opt3.FormattedDisplayText);

        var optAuto = new ScewinOption { ValueHex = "FF", DisplayText = "Auto" };
        Assert.Equal("Auto", optAuto.FormattedDisplayText);
    }

    [Fact]
    public void MemoryTiers_DetermineMemoryTier_ClassifiesCorrectly()
    {
        // Tier 1: AMD CBS
        var cbsToken = new ScewinToken
        {
            TokenId = "2C",
            Offset = "4A",
            Question = "Tcl",
            Category = "Memory"
        };
        cbsToken.MemoryTier = ScewinParser.DetermineMemoryTier(cbsToken);
        Assert.Equal(MemoryTier.Tier1Cbs, cbsToken.MemoryTier);
        Assert.True(cbsToken.IsTier1Cbs);
        Assert.Equal("Основной / AMD CBS", cbsToken.MemoryTierBadgeText);
        Assert.NotNull(cbsToken.InfoTooltipText);
        Assert.Contains("10h Clk = 16", cbsToken.InfoTooltipText);

        // Tier 2: MSI Click BIOS OC Engine
        var msiToken = new ScewinToken
        {
            TokenId = "2A28",
            Offset = "4D8",
            Question = "tCL 16",
            Category = "Memory",
            CustomNumericValue = "0"
        };
        msiToken.MemoryTier = ScewinParser.DetermineMemoryTier(msiToken);
        Assert.Equal(MemoryTier.Tier2Msi, msiToken.MemoryTier);
        Assert.True(msiToken.IsTier2Msi);
        Assert.Equal("Оверлей MSI BIOS (0 = Auto)", msiToken.MemoryTierBadgeText);
        Assert.NotNull(msiToken.InfoTooltipText);
        Assert.Contains("0 = Auto", msiToken.InfoTooltipText);

        // Tier 3: AMD Overclocking PBS
        var pbsToken = new ScewinToken
        {
            TokenId = "64",
            Offset = "D6",
            Question = "Tcl",
            Category = "Memory"
        };
        pbsToken.MemoryTier = ScewinParser.DetermineMemoryTier(pbsToken);
        Assert.Equal(MemoryTier.Tier3Pbs, pbsToken.MemoryTier);
        Assert.True(pbsToken.IsTier3Pbs);
        Assert.Equal("Служебный дубликат / AMD PBS", pbsToken.MemoryTierBadgeText);
        Assert.NotNull(pbsToken.InfoTooltipText);
        Assert.Contains("AMD Overclocking PBS", pbsToken.InfoTooltipText);
    }

    [Fact]
    public void NonMemoryToken_WithCollidingId_IsNotClassifiedAsMemoryTier()
    {
        // Token 0x2C in non-memory formsets (e.g. PME Turn Off Support at offset 35)
        var pmeToken = new ScewinToken
        {
            TokenId = "2C",
            Offset = "35",
            Question = "   PME Turn Off Support",
            HelpString = "Enable to support sending PME_Turn_Off message to Discrete GPU",
            Category = "Other"
        };
        pmeToken.MemoryTier = ScewinParser.DetermineMemoryTier(pmeToken);
        Assert.Equal(MemoryTier.None, pmeToken.MemoryTier);
        Assert.False(pmeToken.IsTier1Cbs);
        Assert.False(pmeToken.IsTier2Msi);
        Assert.False(pmeToken.IsTier3Pbs);

        // Token 0x2D in non-memory formsets (e.g. D3Cold Force Gen1 at offset 37)
        var d3ColdToken = new ScewinToken
        {
            TokenId = "2D",
            Offset = "37",
            Question = "   D3Cold Force Gen1",
            HelpString = "Force Discrete GPU to Gen1 before entering D3Cold",
            Category = "Other"
        };
        d3ColdToken.MemoryTier = ScewinParser.DetermineMemoryTier(d3ColdToken);
        Assert.Equal(MemoryTier.None, d3ColdToken.MemoryTier);
    }

    [Fact]
    public void RealDump_MemoryTiers_SeparatedAndCuratedProperly()
    {
        var path = GetRealDumpPath();
        Assert.True(File.Exists(path), $"Dump file not found at {path}");

        var content = File.ReadAllText(path);
        var dump = _parser.Parse(content, path);

        var vm = new MainViewModel(
            _parser,
            new ScewinDetector(),
            new ScewinRunner(),
            new SettingsService(),
            new ScewinDownloader(),
            LocalizationService.Instance);

        vm.SetCurrentDump(dump);

        Assert.True(vm.HasPrimaryCbsTokens);
        Assert.True(vm.HasMsiOverlayTokens);
        Assert.True(vm.HasPbsDuplicateTokens);

        // Check Tier 1 top 5 tokens (2C, 2D, 2E, 2F, 30)
        Assert.True(vm.PrimaryCbsMemoryTokens.Count >= 5);
        Assert.Equal("2C", vm.PrimaryCbsMemoryTokens[0].TokenId);
        Assert.Equal("Tcl", vm.PrimaryCbsMemoryTokens[0].Question);
        Assert.Equal("4A", vm.PrimaryCbsMemoryTokens[0].Offset);

        Assert.Equal("2D", vm.PrimaryCbsMemoryTokens[1].TokenId);
        Assert.Equal("Trcdrd", vm.PrimaryCbsMemoryTokens[1].Question);
        Assert.Equal("4B", vm.PrimaryCbsMemoryTokens[1].Offset);

        Assert.Equal("2E", vm.PrimaryCbsMemoryTokens[2].TokenId);
        Assert.Equal("Trcdwr", vm.PrimaryCbsMemoryTokens[2].Question);
        Assert.Equal("4C", vm.PrimaryCbsMemoryTokens[2].Offset);

        Assert.Equal("2F", vm.PrimaryCbsMemoryTokens[3].TokenId);
        Assert.Equal("Trp", vm.PrimaryCbsMemoryTokens[3].Question);
        Assert.Equal("4D", vm.PrimaryCbsMemoryTokens[3].Offset);

        Assert.Equal("30", vm.PrimaryCbsMemoryTokens[4].TokenId);
        Assert.Equal("Tras", vm.PrimaryCbsMemoryTokens[4].Question);
        Assert.Equal("4E", vm.PrimaryCbsMemoryTokens[4].Offset);

        foreach (var t in vm.PrimaryCbsMemoryTokens)
        {
            Assert.True(t.IsTier1Cbs);
            Assert.Equal("Основной / AMD CBS", t.MemoryTierBadgeText);
            // Ensure no non-memory token leaked in
            Assert.DoesNotContain("PME Turn Off", t.Question);
            Assert.DoesNotContain("D3Cold", t.Question);
        }

        // Check Tier 2 top 5 MSI tokens (2A28, 2A29, 2A2A, 2A2B, 2A2C)
        Assert.True(vm.MsiOverlayMemoryTokens.Count >= 5);
        Assert.Equal("2A28", vm.MsiOverlayMemoryTokens[0].TokenId);
        Assert.Contains("tCL", vm.MsiOverlayMemoryTokens[0].Question);
        Assert.Equal("4D8", vm.MsiOverlayMemoryTokens[0].Offset);

        Assert.Equal("2A29", vm.MsiOverlayMemoryTokens[1].TokenId);
        Assert.Contains("tRCDRD", vm.MsiOverlayMemoryTokens[1].Question);
        Assert.Equal("4D9", vm.MsiOverlayMemoryTokens[1].Offset);

        Assert.Equal("2A2A", vm.MsiOverlayMemoryTokens[2].TokenId);
        Assert.Contains("tRCDWR", vm.MsiOverlayMemoryTokens[2].Question);
        Assert.Equal("4DA", vm.MsiOverlayMemoryTokens[2].Offset);

        Assert.Equal("2A2B", vm.MsiOverlayMemoryTokens[3].TokenId);
        Assert.Contains("tRP", vm.MsiOverlayMemoryTokens[3].Question);
        Assert.Equal("4DB", vm.MsiOverlayMemoryTokens[3].Offset);

        Assert.Equal("2A2C", vm.MsiOverlayMemoryTokens[4].TokenId);
        Assert.Contains("tRAS", vm.MsiOverlayMemoryTokens[4].Question);
        Assert.Equal("4DC", vm.MsiOverlayMemoryTokens[4].Offset);

        foreach (var t in vm.MsiOverlayMemoryTokens)
        {
            Assert.True(t.IsTier2Msi);
            Assert.Equal("Оверлей MSI BIOS (0 = Auto)", t.MemoryTierBadgeText);
        }

        // Check Tier 3 top 5 PBS tokens (64, 65, 66, 67, 68)
        Assert.True(vm.PbsDuplicateMemoryTokens.Count >= 5);
        Assert.Equal("64", vm.PbsDuplicateMemoryTokens[0].TokenId);
        Assert.Equal("Tcl", vm.PbsDuplicateMemoryTokens[0].Question);
        Assert.Equal("D6", vm.PbsDuplicateMemoryTokens[0].Offset);

        Assert.Equal("65", vm.PbsDuplicateMemoryTokens[1].TokenId);
        Assert.Equal("Trcdrd", vm.PbsDuplicateMemoryTokens[1].Question);
        Assert.Equal("D7", vm.PbsDuplicateMemoryTokens[1].Offset);

        Assert.Equal("66", vm.PbsDuplicateMemoryTokens[2].TokenId);
        Assert.Equal("Trcdwr", vm.PbsDuplicateMemoryTokens[2].Question);
        Assert.Equal("D8", vm.PbsDuplicateMemoryTokens[2].Offset);

        Assert.Equal("67", vm.PbsDuplicateMemoryTokens[3].TokenId);
        Assert.Equal("Trp", vm.PbsDuplicateMemoryTokens[3].Question);
        Assert.Equal("D9", vm.PbsDuplicateMemoryTokens[3].Offset);

        Assert.Equal("68", vm.PbsDuplicateMemoryTokens[4].TokenId);
        Assert.Equal("Tras", vm.PbsDuplicateMemoryTokens[4].Question);
        Assert.Equal("DA", vm.PbsDuplicateMemoryTokens[4].Offset);

        foreach (var t in vm.PbsDuplicateMemoryTokens)
        {
            Assert.True(t.IsTier3Pbs);
            Assert.Equal("Служебный дубликат / AMD PBS", t.MemoryTierBadgeText);
        }
    }

    [Fact]
    public void MemoryViewAndTokenCard_ContainsExpectedBadgesAndExpanders()
    {
        var testDir = AppDomain.CurrentDomain.BaseDirectory;
        var dir = new DirectoryInfo(testDir);
        string? root = null;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SCEWIN_Studio.sln")))
            {
                root = dir.FullName;
                break;
            }
            dir = dir.Parent;
        }
        Assert.NotNull(root);

        var memViewPath = Path.Combine(root, "src", "SCEWIN_Studio", "Views", "MemoryView.xaml");
        Assert.True(File.Exists(memViewPath));
        var memXaml = File.ReadAllText(memViewPath);

        // Verify expanders with exact required headers
        Assert.Contains("Header=\"⚙️ Оверлей MSI Click BIOS (числовой ввод, 0 = Auto)\"", memXaml);
        Assert.Contains("Header=\"📦 Служебные дубликаты AMD Overclocking (по умолчанию Auto)\"", memXaml);
        Assert.Contains("IsExpanded=\"False\"", memXaml);

        // Verify badges in TokenCard.xaml
        var tokenCardPath = Path.Combine(root, "src", "SCEWIN_Studio", "Views", "Controls", "TokenCard.xaml");
        Assert.True(File.Exists(tokenCardPath));
        var cardXaml = File.ReadAllText(tokenCardPath);

        Assert.Contains("Основной / AMD CBS", cardXaml);
        Assert.Contains("Оверлей MSI BIOS (0 = Auto)", cardXaml);
        Assert.Contains("Служебный дубликат / AMD PBS", cardXaml);
    }

    [Fact]
    public void TimingSubtitle_And_IsZeroAuto_Behavior()
    {
        // Tier 1 CBS with hex clock options
        var cbs = new ScewinToken
        {
            TokenId = "2C",
            Offset = "4A",
            Question = "Tcl",
            Category = "Memory",
            MemoryTier = MemoryTier.Tier1Cbs,
            Options = new()
            {
                new ScewinOption { ValueHex = "10", DisplayText = "10h Clk" },
                new ScewinOption { ValueHex = "12", DisplayText = "12h Clk" }
            }
        };
        Assert.True(cbs.HasHexClockOptions);
        Assert.True(cbs.HasTimingSubtitle);
        Assert.Contains("10h = 16", cbs.TimingSubtitle);
        Assert.Equal("\uE950", cbs.IconGlyph);

        // Tier 2 MSI Overlay with 0 = Auto
        var msi = new ScewinToken
        {
            TokenId = "2A28",
            Offset = "4D8",
            Question = "tCL 16",
            Category = "Memory",
            MemoryTier = MemoryTier.Tier2Msi,
            OriginalNumericValue = "0",
            CustomNumericValue = "0"
        };
        Assert.True(msi.IsZeroAuto);
        Assert.True(msi.HasTimingSubtitle);
        Assert.Contains("0 = режим Auto", msi.TimingSubtitle);
        Assert.Equal("\uE950", msi.IconGlyph);

        // Change MSI value to 16 -> IsZeroAuto becomes false
        msi.CustomNumericValue = "16";
        Assert.False(msi.IsZeroAuto);
        Assert.True(msi.IsModified);

        // Reset -> IsZeroAuto becomes true again
        msi.Reset();
        Assert.True(msi.IsZeroAuto);
        Assert.False(msi.IsModified);
    }

    [Fact]
    public void NumericStepper_ClampingAtZero_PreventsNegativeValues()
    {
        var token = new ScewinToken
        {
            CustomNumericValue = "0"
        };

        token.DecrementNumeric();
        Assert.Equal("0", token.CustomNumericValue);

        token.IncrementNumeric();
        Assert.Equal("1", token.CustomNumericValue);

        token.DecrementNumeric();
        Assert.Equal("0", token.CustomNumericValue);
    }
}

