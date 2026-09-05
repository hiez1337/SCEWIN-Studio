using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using SCEWIN_Studio.Services;
using Xunit;

namespace SCEWIN_Studio.Tests;

public class LocalizationTests
{
    [Fact]
    public void RussianAndEnglishJson_HaveIdenticalKeys()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var ruPath = Path.Combine(appDir, "Resources", "Strings.ru.json");
        var enPath = Path.Combine(appDir, "Resources", "Strings.en.json");

        if (!File.Exists(ruPath))
        {
            // Try relative path from test execution directory
            ruPath = Path.GetFullPath(Path.Combine(appDir, "..", "..", "..", "..", "src", "SCEWIN_Studio", "Resources", "Strings.ru.json"));
            enPath = Path.GetFullPath(Path.Combine(appDir, "..", "..", "..", "..", "src", "SCEWIN_Studio", "Resources", "Strings.en.json"));
        }

        Assert.True(File.Exists(ruPath), $"File not found: {ruPath}");
        Assert.True(File.Exists(enPath), $"File not found: {enPath}");

        var ruDict = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(ruPath))!;
        var enDict = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(enPath))!;

        Assert.NotEmpty(ruDict);
        Assert.NotEmpty(enDict);

        // Check key parity
        foreach (var key in ruDict.Keys)
        {
            Assert.True(enDict.ContainsKey(key), $"English JSON is missing key: '{key}' found in Russian JSON");
            Assert.False(string.IsNullOrWhiteSpace(enDict[key]), $"English value for '{key}' is empty");
        }

        foreach (var key in enDict.Keys)
        {
            Assert.True(ruDict.ContainsKey(key), $"Russian JSON is missing key: '{key}' found in English JSON");
            Assert.False(string.IsNullOrWhiteSpace(ruDict[key]), $"Russian value for '{key}' is empty");
        }
    }

    [Fact]
    public void LocalizationService_SwitchesLanguagesProperly()
    {
        var service = new LocalizationService();

        service.CurrentLanguage = "ru-RU";
        Assert.True(service.IsRussian);
        Assert.False(service.IsEnglish);
        var ruTitle = service["Nav_Dashboard"];

        service.CurrentLanguage = "en-US";
        Assert.False(service.IsRussian);
        Assert.True(service.IsEnglish);
        var enTitle = service["Nav_Dashboard"];

        Assert.Equal("Панель управления", ruTitle);
        Assert.Equal("Dashboard", enTitle);
    }
}
