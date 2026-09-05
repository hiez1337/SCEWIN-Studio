using System;
using System.IO;
using System.Linq;
using SCEWIN_Studio.Services;
using Xunit;

namespace SCEWIN_Studio.Tests;

public class RealDumpIntegrationTests
{
    private readonly ScewinParser _parser = new();

    [Fact]
    public void ParseRealDump_NvramBefore_SuccessfullyParsesAllTokens()
    {
        var candidates = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "nvramBEFORE.txt"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nvramBEFORE.txt"),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "src", "SCEWIN_Studio", "nvramBEFORE.txt")),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Скрипты и Конфиги", "SCEHUB-main", "SCEHUB-main", "SCEWIN", "5.05.01.0002", "nvramBEFORE.txt")
        };
        var testDataPath = candidates.FirstOrDefault(File.Exists) ?? candidates[0];

        Assert.True(File.Exists(testDataPath), $"Test dump file not found: {testDataPath}");

        var content = File.ReadAllText(testDataPath);
        var dump = _parser.Parse(content, testDataPath);

        Assert.Equal("8F23D09B", dump.HiiCrc32);
        Assert.Equal("5.05.01.0002", dump.UtilityVersion);
        Assert.True(dump.Tokens.Count > 500, $"Expected > 500 tokens, got {dump.Tokens.Count}");

        // Verify known setup questions from the dump
        var cpuRev = dump.Tokens.FirstOrDefault(t => t.Question == "CPU Revision");
        Assert.NotNull(cpuRev);
        Assert.Equal("02", cpuRev.TokenId);

        var lanPower = dump.Tokens.FirstOrDefault(t => t.Question == "LAN Power Enable");
        Assert.NotNull(lanPower);
        Assert.Equal("05", lanPower.TokenId);
        Assert.NotNull(lanPower.OriginalOption);
        Assert.Equal("01", lanPower.OriginalOption!.ValueHex);

        // Test modifying a real token and generating diff
        lanPower.CurrentOption = lanPower.Options.First(o => o.ValueHex == "00");
        Assert.True(lanPower.IsModified);

        var diff = _parser.GenerateDiffScript(dump, new[] { lanPower });
        Assert.Contains("HIICrc32= 8F23D09B", diff);
        Assert.Contains("Setup Question\t= LAN Power Enable", diff);
        Assert.Contains("*[00]Disabled", diff);
    }
}
