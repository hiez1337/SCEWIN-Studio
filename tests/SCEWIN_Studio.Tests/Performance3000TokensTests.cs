using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SCEWIN_Studio.Models;
using SCEWIN_Studio.Services;
using SCEWIN_Studio.ViewModels;
using Xunit;

namespace SCEWIN_Studio.Tests;

public class Performance3000TokensTests
{
    private readonly ScewinParser _parser = new();

    private string GenerateLargeDumpText(int tokenCount = 3500)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// AMI Setup Control Environment (AMISCE) Utility. Ver 5.05.01.0002");
        sb.AppendLine("// Created on 09/06/2026 12:00:00");
        sb.AppendLine("HIICrc32 = A1B2C3D4");
        sb.AppendLine();

        for (int i = 0; i < tokenCount; i++)
        {
            sb.AppendLine($"Setup Question\t= Parameter {i:D4}");
            sb.AppendLine($"Help String\t= Description for parameter {i:D4} with PCIe and overclocking hints");
            sb.AppendLine($"Token\t= {i:X4}");
            sb.AppendLine($"Offset\t= {i * 2:X4}");
            sb.AppendLine("Width\t= 01");
            sb.AppendLine("BIOS Default\t= [00]Disabled");
            sb.AppendLine("Options\t= *[00]Disabled // [00]");
            sb.AppendLine("\t[01]Enabled // [01]");
            sb.AppendLine("\t[02]Auto // [02]");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    [Fact]
    public void Parse_3500Tokens_CompletesWithinBudget()
    {
        var dumpText = GenerateLargeDumpText(3500);

        var sw = Stopwatch.StartNew();
        var dump = _parser.Parse(dumpText, "benchmark.txt");
        sw.Stop();

        Assert.Equal(3500, dump.Tokens.Count);
        Assert.True(sw.ElapsedMilliseconds < 1500, $"Parsing 3500 tokens took {sw.ElapsedMilliseconds} ms (budget < 1500 ms)");
    }

    [Fact]
    public void SetCurrentDump_4000Tokens_PopulatesCollectionsRapidly()
    {
        var dumpText = GenerateLargeDumpText(4000);
        var dump = _parser.Parse(dumpText, "benchmark4000.txt");

        var vm = new MainViewModel();
        var sw = Stopwatch.StartNew();
        vm.SetCurrentDump(dump);
        sw.Stop();

        Assert.Equal(4000, vm.AllTokens.Count);
        Assert.True(sw.ElapsedMilliseconds < 500, $"SetCurrentDump with 4000 tokens took {sw.ElapsedMilliseconds} ms (budget < 500 ms)");
    }

    [Fact]
    public void SetCurrentDump_DetachesOldDumpTokenEvents()
    {
        var dump1 = _parser.Parse(GenerateLargeDumpText(100), "dump1.txt");
        var dump2 = _parser.Parse(GenerateLargeDumpText(100), "dump2.txt");

        var vm = new MainViewModel();
        vm.SetCurrentDump(dump1);

        var tokenFromDump1 = dump1.Tokens[0];
        tokenFromDump1.CurrentOption = tokenFromDump1.Options[1];
        Assert.True(vm.HasModifiedItems);
        Assert.Equal(1, vm.ModifiedCount);

        // Switch to dump2
        vm.SetCurrentDump(dump2);
        Assert.False(vm.HasModifiedItems);

        // Modifying tokenFromDump1 should NO LONGER trigger notifications in vm
        tokenFromDump1.CurrentOption = tokenFromDump1.Options[2];
        Assert.False(vm.HasModifiedItems);
        Assert.Equal(0, vm.ModifiedCount);
    }

    [Fact]
    public async Task SearchFilterDebounced_4000Tokens_FiltersCorrectly()
    {
        var dumpText = GenerateLargeDumpText(4000);
        var dump = _parser.Parse(dumpText, "benchmark4000.txt");

        var vm = new MainViewModel();
        vm.SetCurrentDump(dump);

        vm.RawSearchQuery = "0042";
        vm.TriggerRawFilterDebounced(overrideDelayMs: 10);

        if (vm.CurrentSearchTask != null)
        {
            await vm.CurrentSearchTask;
        }

        Assert.Contains(vm.FilteredRawTokens, t => t.Question.Contains("0042"));
    }

    [Fact]
    public void EmbeddedLocalizationResources_ExistInAssemblyManifest()
    {
        var assembly = typeof(ScewinToken).Assembly;
        var names = assembly.GetManifestResourceNames();

        Assert.Contains("SCEWIN_Studio.Resources.Strings.ru.json", names);
        Assert.Contains("SCEWIN_Studio.Resources.Strings.en.json", names);
        Assert.Contains("SCEWIN_Studio.Resources.SCEWIN.zip", names);
    }

    [Fact]
    public void LocalizationService_CanLoadEmbeddedStrings()
    {
        var service = new LocalizationService();

        service.CurrentLanguage = "en";
        var enDashboard = service["Nav_Dashboard"];
        Assert.False(string.IsNullOrEmpty(enDashboard));

        service.CurrentLanguage = "ru";
        var ruDashboard = service["Nav_Dashboard"];
        Assert.False(string.IsNullOrEmpty(ruDashboard));
        Assert.NotEqual(enDashboard, ruDashboard);
    }

    [Fact]
    public void TokenCard_PropertiesAreCachedAndDoNotReallocate()
    {
        var token = new ScewinToken
        {
            Question = "PM L1 SS",
            HelpString = "PCI Express Active State Power Management L1 Substates",
            TokenId = "005A",
            Offset = "0020"
        };
        token.Options.Add(new ScewinOption { ValueHex = "00", DisplayText = "Disabled" });
        token.Options.Add(new ScewinOption { ValueHex = "01", DisplayText = "Enabled", IsSelected = true });
        token.OriginalOption = token.Options[1];
        token.CurrentOption = token.Options[1];

        // 1. SearchableLower caching
        var s1 = token.SearchableLower;
        var s2 = token.SearchableLower;
        Assert.Same(s1, s2);

        // 2. DisplayTitle caching & normalization
        Assert.Equal("Active State Power Management (ASPM)", token.DisplayTitle);
        var t1 = token.DisplayTitle;
        var t2 = token.DisplayTitle;
        Assert.Same(t1, t2);

        // 3. IsBinaryToggle
        Assert.True(token.IsBinaryToggle);

        // 4. IconGlyph
        var g1 = token.IconGlyph;
        var g2 = token.IconGlyph;
        Assert.Same(g1, g2);

        // 5. IsOptimal
        Assert.False(token.IsOptimal);
        token.CurrentOption = token.Options[0]; // Disabled is optimal for ASPM
        Assert.True(token.IsOptimal);
    }
}
