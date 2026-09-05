using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SCEWIN_Studio.Models;
using SCEWIN_Studio.Services;
using SCEWIN_Studio.ViewModels;
using Xunit;

namespace SCEWIN_Studio.Tests;

public class DumpComparerTests
{
    private readonly ScewinParser _parser = new();
    private readonly DumpComparer _comparer = new();

    private const string SampleDumpA = @"HIICrc32= 1234ABCD

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
Options	=*[00]Disabled
         [01]Enabled

Setup Question	= Only In A Setting
Help String	= Setting only present in Dump A
Token	=07
Offset	=15
Width	=01
Options	=*[00]Disabled
         [01]Enabled
";

    private const string SampleDumpB = @"HIICrc32= 5678EF01

Setup Question	= LAN Power Enable
Help String	= Enable or disable LAN Power
Token	=05
Offset	=13
Width	=01
Options	=*[00]Disabled
         [01]Enabled

Setup Question	= WLAN Enable
Help String	= Enable or disable WLAN
Token	=06
Offset	=14
Width	=01
Options	=*[00]Disabled
         [01]Enabled

Setup Question	= Only In B Setting
Help String	= Setting only present in Dump B
Token	=08
Offset	=16
Width	=01
Options	=*[01]Enabled
         [00]Disabled
";

    [Fact]
    public void Compare_IdenticalDumps_AllItemsEqual()
    {
        var dump1 = _parser.Parse(SampleDumpA);
        var dump2 = _parser.Parse(SampleDumpA);

        var results = _comparer.Compare(dump1, dump2);

        Assert.Equal(3, results.Count);
        Assert.All(results, item => Assert.Equal(ComparisonStatus.Equal, item.Status));
        Assert.All(results, item => Assert.True(item.IsEqual));
        Assert.All(results, item => Assert.False(item.IsDifferent));
    }

    [Fact]
    public void Compare_DifferentAndExclusiveSettings_CorrectlyIdentifiedAndSorted()
    {
        var dumpA = _parser.Parse(SampleDumpA);
        var dumpB = _parser.Parse(SampleDumpB);

        var results = _comparer.Compare(dumpA, dumpB);

        Assert.Equal(4, results.Count);

        // Sorting rule: Different first, then OnlyInA / OnlyInB, then Equal
        Assert.Equal(ComparisonStatus.Different, results[0].Status);
        Assert.Equal("LAN Power Enable", results[0].Question);
        Assert.Equal("Enabled", results[0].ValueA);
        Assert.Equal("Disabled", results[0].ValueB);
        Assert.True(results[0].IsDifferent);

        // Check OnlyInA and OnlyInB are in middle
        var onlyInA = results.FirstOrDefault(r => r.Status == ComparisonStatus.OnlyInA);
        Assert.NotNull(onlyInA);
        Assert.Equal("Only In A Setting", onlyInA!.Question);
        Assert.Equal("Disabled", onlyInA.ValueA);
        Assert.Equal("—", onlyInA.ValueB);

        var onlyInB = results.FirstOrDefault(r => r.Status == ComparisonStatus.OnlyInB);
        Assert.NotNull(onlyInB);
        Assert.Equal("Only In B Setting", onlyInB!.Question);
        Assert.Equal("—", onlyInB.ValueA);
        Assert.Equal("Enabled", onlyInB.ValueB);

        // Check Equal is at the end
        var equalItem = results.Last();
        Assert.Equal(ComparisonStatus.Equal, equalItem.Status);
        Assert.Equal("WLAN Enable", equalItem.Question);
        Assert.Equal("Disabled", equalItem.ValueA);
        Assert.Equal("Disabled", equalItem.ValueB);
    }

    [Fact]
    public void Compare_DuplicateQuestions_MatchesSequentially()
    {
        const string dumpMultiA = @"Setup Question	= ASPM Support
Token	=10
Offset	=20
Width	=01
Options	=*[00]Disabled
         [01]Enabled

Setup Question	= ASPM Support
Token	=11
Offset	=21
Width	=01
Options	=[00]Disabled
         *[01]Enabled
";

        const string dumpMultiB = @"Setup Question	= ASPM Support
Token	=20
Offset	=30
Width	=01
Options	=*[00]Disabled
         [01]Enabled

Setup Question	= ASPM Support
Token	=21
Offset	=31
Width	=01
Options	=*[00]Disabled
         [01]Enabled
";

        var dumpA = _parser.Parse(dumpMultiA);
        var dumpB = _parser.Parse(dumpMultiB);

        var results = _comparer.Compare(dumpA, dumpB);

        Assert.Equal(2, results.Count);
        // First occurrence: Disabled in both -> Equal
        // Second occurrence: Enabled in A, Disabled in B -> Different
        // Due to sort order, Different comes before Equal
        Assert.Equal(ComparisonStatus.Different, results[0].Status);
        Assert.Equal("Enabled", results[0].ValueA);
        Assert.Equal("Disabled", results[0].ValueB);

        Assert.Equal(ComparisonStatus.Equal, results[1].Status);
        Assert.Equal("Disabled", results[1].ValueA);
        Assert.Equal("Disabled", results[1].ValueB);
    }

    [Fact]
    public void Compare_RealB550AndFriendAM5Dump_ProducesValidComparison()
    {
        var testDataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData");
        var fileB550 = Path.Combine(testDataDir, "nvramBEFORE.txt");
        var fileAM5 = Path.Combine(testDataDir, "nvram_dump_20260905_204705.txt");

        if (!File.Exists(fileAM5))
        {
            // Fallback candidate locations
            var candidate = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "nvram_dump_20260905_204705.txt"));
            if (File.Exists(candidate))
            {
                fileAM5 = candidate;
            }
        }

        Assert.True(File.Exists(fileB550), $"B550 file not found at {fileB550}");
        Assert.True(File.Exists(fileAM5), $"AM5 friend file not found at {fileAM5}");

        var dumpB550 = _parser.Parse(File.ReadAllText(fileB550), fileB550);
        var dumpAM5 = _parser.Parse(File.ReadAllText(fileAM5), fileAM5);

        Assert.True(dumpB550.Tokens.Count > 1000);
        Assert.True(dumpAM5.Tokens.Count > 1000);

        var results = _comparer.Compare(dumpB550, dumpAM5);

        Assert.True(results.Count > 1000);

        var differentCount = results.Count(r => r.IsDifferent);
        var equalCount = results.Count(r => r.IsEqual);

        Assert.True(differentCount > 0, "Expected differences between B550 and AM5 dumps");
        Assert.True(equalCount > 0, "Expected some shared common defaults between dumps");

        // Verify sorting: all Different items come before Equal items
        var firstEqualIndex = results.FindIndex(r => r.IsEqual);
        var lastDifferentIndex = results.FindLastIndex(r => r.IsDifferent);
        if (firstEqualIndex >= 0 && lastDifferentIndex >= 0)
        {
            Assert.True(lastDifferentIndex < firstEqualIndex, "All Different items must precede Equal items");
        }
    }

    [Fact]
    public void MainViewModel_RunComparison_PopulatesCollectionsAndCounts()
    {
        var vm = new MainViewModel();
        var dumpA = _parser.Parse(SampleDumpA, "path/dumpA.txt");
        var dumpB = _parser.Parse(SampleDumpB, "path/dumpB.txt");

        vm.ComparisonDumpA = dumpA;
        vm.ComparisonDumpB = dumpB;

        Assert.True(vm.CanRunComparison);
        Assert.False(vm.HasComparisonResults);

        vm.RunComparisonCommand.Execute(null);

        Assert.True(vm.HasComparisonResults);
        Assert.Equal(4, vm.TotalComparedCount);
        Assert.Equal(1, vm.DifferentCount);
        Assert.Equal(1, vm.EqualCount);
        Assert.Equal(1, vm.OnlyInACount);
        Assert.Equal(1, vm.OnlyInBCount);

        Assert.Equal(4, vm.AllComparisonItems.Count);
        Assert.Equal(4, vm.FilteredComparisonItems.Count);

        // Test filtering by status
        vm.SetComparisonFilter("Different");
        Assert.Single(vm.FilteredComparisonItems);
        Assert.Equal(ComparisonStatus.Different, vm.FilteredComparisonItems[0].Status);

        vm.SetComparisonFilter("Equal");
        Assert.Single(vm.FilteredComparisonItems);
        Assert.Equal(ComparisonStatus.Equal, vm.FilteredComparisonItems[0].Status);

        vm.SetComparisonFilter("OnlyA");
        Assert.Single(vm.FilteredComparisonItems);
        Assert.Equal(ComparisonStatus.OnlyInA, vm.FilteredComparisonItems[0].Status);

        vm.SetComparisonFilter("OnlyB");
        Assert.Single(vm.FilteredComparisonItems);
        Assert.Equal(ComparisonStatus.OnlyInB, vm.FilteredComparisonItems[0].Status);

        // Reset filter to All and test search query
        vm.SetComparisonFilter("All");
        Assert.Equal(4, vm.FilteredComparisonItems.Count);

        vm.ComparisonSearchQuery = "LAN Power";
        vm.UpdateComparisonFilter(immediate: true);
        Assert.Single(vm.FilteredComparisonItems);
        Assert.Equal("LAN Power Enable", vm.FilteredComparisonItems[0].Question);
    }

    [Fact]
    public void MainViewModel_UseCurrentDumpAsA_SetsComparisonDumpA()
    {
        var vm = new MainViewModel();
        var dump = _parser.Parse(SampleDumpA, "C:\\bios\\my_b550_dump.txt");

        vm.SetCurrentDump(dump);

        // When dump is loaded, ComparisonDumpA is automatically initialized or set via command
        Assert.NotNull(vm.ComparisonDumpA);
        Assert.Equal(dump, vm.ComparisonDumpA);

        // Now test UseCurrentDumpAsACommand explicitly
        vm.ComparisonDumpA = null;
        vm.UseCurrentDumpAsACommand.Execute(null);

        Assert.NotNull(vm.ComparisonDumpA);
        Assert.Equal(dump, vm.ComparisonDumpA);
        Assert.Contains("my_b550_dump.txt", vm.ComparisonDumpAInfo);
    }
}
