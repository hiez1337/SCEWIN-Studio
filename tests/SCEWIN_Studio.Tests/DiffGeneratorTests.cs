using System.Linq;
using SCEWIN_Studio.Models;
using SCEWIN_Studio.Services;
using Xunit;

namespace SCEWIN_Studio.Tests;

public class DiffGeneratorTests
{
    private readonly ScewinParser _parser = new();

    private const string SampleDump = @"HIICrc32= 8F23D09B

Setup Question	= LAN Power Enable
Help String	= Enable or disable LAN Power
Token	=05
Offset	=13
Width	=01
Options	=[00]Disabled
         *[01]Enabled

Setup Question	= WLAN Enable
Help String	= Enable or disable WLAN
Token	=06
Offset	=14
Width	=01
Options	=[00]Disabled
         *[01]Enabled
";

    [Fact]
    public void GenerateDiffScript_IncludesOnlyModifiedTokensWithCorrectAsterisk()
    {
        var dump = _parser.Parse(SampleDump);

        // Modify token 05 from [01] to [00]
        dump.Tokens[0].CurrentOption = dump.Tokens[0].Options.First(o => o.ValueHex == "00");

        var modifiedTokens = dump.Tokens.Where(t => t.IsModified).ToList();
        Assert.Single(modifiedTokens);

        var diffScript = _parser.GenerateDiffScript(dump, modifiedTokens);

        Assert.Contains("HIICrc32= 8F23D09B", diffScript);
        Assert.Contains("Setup Question\t= LAN Power Enable", diffScript);
        Assert.Contains("Token\t=05", diffScript);
        Assert.Contains("*[00]Disabled", diffScript);
        Assert.Contains("[01]Enabled", diffScript);
        Assert.DoesNotContain("Setup Question\t= WLAN Enable", diffScript);
    }
}
