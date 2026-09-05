using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using SCEWIN_Studio.Collections;
using SCEWIN_Studio.Models;
using SCEWIN_Studio.Services;
using SCEWIN_Studio.ViewModels;
using Xunit;

namespace SCEWIN_Studio.Tests;

public class ObservableRangeCollectionTests
{
    [Fact]
    public void ReplaceRange_WithMultipleItems_RaisesOnlyOneResetEventAndPopulatesItems()
    {
        var collection = new ObservableRangeCollection<string>();
        int collectionChangedCount = 0;
        NotifyCollectionChangedAction? lastAction = null;
        int countPropertyChangedCount = 0;
        int indexerPropertyChangedCount = 0;

        collection.CollectionChanged += (s, e) =>
        {
            collectionChangedCount++;
            lastAction = e.Action;
        };

        ((INotifyPropertyChanged)collection).PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(collection.Count))
            {
                countPropertyChangedCount++;
            }
            else if (e.PropertyName == "Item[]")
            {
                indexerPropertyChangedCount++;
            }
        };

        var newItems = new[] { "Alpha", "Bravo", "Charlie", "Delta" };
        collection.ReplaceRange(newItems);

        Assert.Equal(1, collectionChangedCount);
        Assert.Equal(NotifyCollectionChangedAction.Reset, lastAction);
        Assert.Equal(1, countPropertyChangedCount);
        Assert.Equal(1, indexerPropertyChangedCount);
        Assert.Equal(4, collection.Count);
        Assert.Equal(newItems, collection);
    }

    [Fact]
    public void ReplaceRange_ClearsPreviousItems_AndPopulatesNewItems()
    {
        var collection = new ObservableRangeCollection<string>(new[] { "Old1", "Old2", "Old3" });
        int collectionChangedCount = 0;

        collection.CollectionChanged += (s, e) =>
        {
            collectionChangedCount++;
            Assert.Equal(NotifyCollectionChangedAction.Reset, e.Action);
        };

        var newItems = new[] { "New1", "New2" };
        collection.ReplaceRange(newItems);

        Assert.Equal(1, collectionChangedCount);
        Assert.Equal(2, collection.Count);
        Assert.Equal("New1", collection[0]);
        Assert.Equal("New2", collection[1]);
    }

    [Fact]
    public void ReplaceRange_WithEmptyCollection_ClearsItemsAndRaisesSingleResetEvent()
    {
        var collection = new ObservableRangeCollection<int>(new[] { 1, 2, 3 });
        int collectionChangedCount = 0;

        collection.CollectionChanged += (s, e) =>
        {
            collectionChangedCount++;
            Assert.Equal(NotifyCollectionChangedAction.Reset, e.Action);
        };

        collection.ReplaceRange(Array.Empty<int>());

        Assert.Equal(1, collectionChangedCount);
        Assert.Empty(collection);
    }

    [Fact]
    public void ReplaceRange_NullCollection_ThrowsArgumentNullException()
    {
        var collection = new ObservableRangeCollection<string>();
        Assert.Throws<ArgumentNullException>(() => collection.ReplaceRange(null!));
    }

    [Fact]
    public void ReplaceRange_SelfReference_DoesNotClearCollection()
    {
        var collection = new ObservableRangeCollection<string>(new[] { "Item1", "Item2" });
        int collectionChangedCount = 0;

        collection.CollectionChanged += (s, e) => collectionChangedCount++;

        collection.ReplaceRange(collection);

        Assert.Equal(0, collectionChangedCount);
        Assert.Equal(2, collection.Count);
        Assert.Equal("Item1", collection[0]);
    }

    [Fact]
    public void AddRange_WithMultipleItems_RaisesSingleResetEventAndAppendsItems()
    {
        var collection = new ObservableRangeCollection<int>(new[] { 10, 20 });
        int collectionChangedCount = 0;
        int countPropertyChangedCount = 0;

        collection.CollectionChanged += (s, e) =>
        {
            collectionChangedCount++;
            Assert.Equal(NotifyCollectionChangedAction.Reset, e.Action);
        };

        ((INotifyPropertyChanged)collection).PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(collection.Count))
            {
                countPropertyChangedCount++;
            }
        };

        collection.AddRange(new[] { 30, 40, 50 });

        Assert.Equal(1, collectionChangedCount);
        Assert.Equal(1, countPropertyChangedCount);
        Assert.Equal(5, collection.Count);
        Assert.Equal(new[] { 10, 20, 30, 40, 50 }, collection);
    }

    [Fact]
    public void AddRange_WithEmptyCollection_DoesNotRaiseEventOrModifyCollection()
    {
        var collection = new ObservableRangeCollection<string>(new[] { "A", "B" });
        int collectionChangedCount = 0;

        collection.CollectionChanged += (s, e) => collectionChangedCount++;

        collection.AddRange(Array.Empty<string>());

        Assert.Equal(0, collectionChangedCount);
        Assert.Equal(2, collection.Count);
    }

    [Fact]
    public void AddRange_NullCollection_ThrowsArgumentNullException()
    {
        var collection = new ObservableRangeCollection<string>();
        Assert.Throws<ArgumentNullException>(() => collection.AddRange(null!));
    }

    private static ScewinDump CreateSampleDump()
    {
        var dump = new ScewinDump();
        dump.Tokens.Add(new ScewinToken
        {
            Question = "Precision Boost Overdrive",
            TokenId = "0x01",
            Offset = "0x0010",
            Category = "Overclocking",
            SubCategory = "PboCurve",
            HelpString = "Configure AMD Precision Boost Overdrive algorithms",
            OriginalNumericValue = "Auto",
            CustomNumericValue = "Enabled"
        });

        dump.Tokens.Add(new ScewinToken
        {
            Question = "Curve Optimizer",
            TokenId = "0x02",
            Offset = "0x0014",
            Category = "Overclocking",
            SubCategory = "PboCurve",
            HelpString = "Per-Core or All-Core Voltage Offset Curves",
            OriginalNumericValue = "Auto",
            CustomNumericValue = "All Cores"
        });

        dump.Tokens.Add(new ScewinToken
        {
            Question = "PPT Limit",
            TokenId = "0x03",
            Offset = "0x0018",
            Category = "Overclocking",
            SubCategory = "PboLimits",
            HelpString = "Package Power Tracking Limit (Watts)",
            OriginalNumericValue = "Auto",
            CustomNumericValue = "142"
        });

        dump.Tokens.Add(new ScewinToken
        {
            Question = "Global C-state Control",
            TokenId = "0x04",
            Offset = "0x0020",
            Category = "CpuPower",
            SubCategory = "CpuPowerStates",
            HelpString = "Enable or disable processor idle C-states",
            OriginalNumericValue = "Enabled",
            CustomNumericValue = "Enabled"
        });

        dump.Tokens.Add(new ScewinToken
        {
            Question = "tCL",
            TokenId = "2A28",
            Offset = "0x0030",
            Category = "Memory",
            SubCategory = "PrimaryTimings",
            HelpString = "CAS Latency Clocks",
            OriginalNumericValue = "Auto",
            CustomNumericValue = "16",
            MemoryTier = MemoryTier.Tier2Msi
        });

        dump.Tokens.Add(new ScewinToken
        {
            Question = "Active State Power Management (ASPM)",
            TokenId = "0x06",
            Offset = "0x0040",
            Category = "ASPM",
            SubCategory = "AspmL1",
            HelpString = "PCI Express Active State Power Management",
            OriginalNumericValue = "Disabled",
            CustomNumericValue = "Disabled"
        });

        return dump;
    }

    private static MainViewModel CreateViewModelWithDump(ScewinDump dump)
    {
        var vm = new MainViewModel(
            new ScewinParser(),
            new ScewinDetector(),
            new ScewinRunner(),
            new SettingsService(),
            new ScewinDownloader(),
            LocalizationService.Instance);
        vm.SearchDebounceDelayMs = 0;
        vm.SetCurrentDump(dump);
        return vm;
    }

    [Fact]
    public async Task SearchFiltering_MatchesQuestion_CaseInsensitively()
    {
        using var vm = CreateViewModelWithDump(CreateSampleDump());

        vm.RawSearchQuery = "optimizer";
        if (vm.CurrentSearchTask != null) await vm.CurrentSearchTask;

        Assert.Single(vm.FilteredRawTokens);
        Assert.Equal("Curve Optimizer", vm.FilteredRawTokens[0].Question);

        // Case-insensitivity test (UPPERCASE)
        vm.RawSearchQuery = "OPTIMIZER";
        if (vm.CurrentSearchTask != null) await vm.CurrentSearchTask;

        Assert.Single(vm.FilteredRawTokens);
        Assert.Equal("Curve Optimizer", vm.FilteredRawTokens[0].Question);

        // Mixed case
        vm.RawSearchQuery = "pReCiSiOn";
        if (vm.CurrentSearchTask != null) await vm.CurrentSearchTask;

        Assert.Single(vm.FilteredRawTokens);
        Assert.Equal("Precision Boost Overdrive", vm.FilteredRawTokens[0].Question);
    }

    [Fact]
    public async Task SearchFiltering_MatchesTokenIdOffsetAndHelpString()
    {
        using var vm = CreateViewModelWithDump(CreateSampleDump());

        // Match by TokenId "2A28"
        vm.RawSearchQuery = "2A28";
        if (vm.CurrentSearchTask != null) await vm.CurrentSearchTask;

        Assert.Single(vm.FilteredRawTokens);
        Assert.Equal("tCL", vm.FilteredRawTokens[0].Question);

        // Match by Offset "0040"
        vm.RawSearchQuery = "0040";
        if (vm.CurrentSearchTask != null) await vm.CurrentSearchTask;

        Assert.Single(vm.FilteredRawTokens);
        Assert.Equal("Active State Power Management (ASPM)", vm.FilteredRawTokens[0].Question);

        // Match by HelpString "Watts"
        vm.RawSearchQuery = "Watts";
        if (vm.CurrentSearchTask != null) await vm.CurrentSearchTask;

        Assert.Single(vm.FilteredRawTokens);
        Assert.Equal("PPT Limit", vm.FilteredRawTokens[0].Question);
    }

    [Fact]
    public async Task SearchFiltering_CategoryAndSubCategory_FiltersCorrectly()
    {
        using var vm = CreateViewModelWithDump(CreateSampleDump());

        // Category filter "Overclocking"
        vm.RawCategoryFilter = "Overclocking";
        if (vm.CurrentSearchTask != null) await vm.CurrentSearchTask;

        Assert.Equal(3, vm.FilteredRawTokens.Count);
        Assert.All(vm.FilteredRawTokens, t => Assert.Equal("Overclocking", t.Category));

        // SubCategory filter "PboCurve"
        vm.RawCategoryFilter = "PboCurve";
        if (vm.CurrentSearchTask != null) await vm.CurrentSearchTask;

        Assert.Equal(2, vm.FilteredRawTokens.Count);
        Assert.Contains(vm.FilteredRawTokens, t => t.Question == "Precision Boost Overdrive");
        Assert.Contains(vm.FilteredRawTokens, t => t.Question == "Curve Optimizer");

        // Category filter "ASPM"
        vm.RawCategoryFilter = "ASPM";
        if (vm.CurrentSearchTask != null) await vm.CurrentSearchTask;

        Assert.Single(vm.FilteredRawTokens);
        Assert.Equal("Active State Power Management (ASPM)", vm.FilteredRawTokens[0].Question);
    }

    [Fact]
    public async Task SearchFiltering_RawOnlyModified_FiltersOnlyModifiedTokens()
    {
        using var vm = CreateViewModelWithDump(CreateSampleDump());

        // Initially 6 total tokens
        Assert.Equal(6, vm.FilteredRawTokens.Count);

        // RawOnlyModified = true
        // In our sample dump:
        // PBO: Current "Enabled", Original "Auto" -> IsModified = true
        // Curve Optimizer: Current "All Cores", Original "Auto" -> IsModified = true
        // PPT Limit: Current "142", Original "Auto" -> IsModified = true
        // Global C-state: Current "Enabled", Original "Enabled" -> IsModified = false
        // tCL: Current "16", Original "Auto" -> IsModified = true
        // ASPM: Current "Disabled", Original "Disabled" -> IsModified = false
        vm.RawOnlyModified = true;
        if (vm.CurrentSearchTask != null) await vm.CurrentSearchTask;

        Assert.Equal(4, vm.FilteredRawTokens.Count);
        Assert.All(vm.FilteredRawTokens, t => Assert.True(t.IsModified));

        // Combine RawOnlyModified with query
        vm.RawSearchQuery = "PPT";
        if (vm.CurrentSearchTask != null) await vm.CurrentSearchTask;

        Assert.Single(vm.FilteredRawTokens);
        Assert.Equal("PPT Limit", vm.FilteredRawTokens[0].Question);
    }

    [Fact]
    public async Task SearchDebouncing_CancelsPreviousQueriesAndAppliesLatestQuery()
    {
        var dump = CreateSampleDump();
        using var vm = new MainViewModel(
            new ScewinParser(),
            new ScewinDetector(),
            new ScewinRunner(),
            new SettingsService(),
            new ScewinDownloader(),
            LocalizationService.Instance);

        vm.SearchDebounceDelayMs = 150;
        vm.SetCurrentDump(dump);

        // Simulate rapid typing: "O", "Op", "Opt", "Optimizer"
        vm.RawSearchQuery = "O";
        var task1 = vm.CurrentSearchTask;

        vm.RawSearchQuery = "Op";
        var task2 = vm.CurrentSearchTask;

        vm.RawSearchQuery = "Opt";
        var task3 = vm.CurrentSearchTask;

        vm.RawSearchQuery = "Optimizer";
        var finalTask = vm.CurrentSearchTask;

        Assert.NotNull(finalTask);
        await finalTask;

        // Final task should be completed and FilteredRawTokens should match "Curve Optimizer"
        Assert.Single(vm.FilteredRawTokens);
        Assert.Equal("Curve Optimizer", vm.FilteredRawTokens[0].Question);
    }

    [Fact]
    public void UpdateRawFilter_Immediate_PopulatesTokensSynchronously()
    {
        var dump = CreateSampleDump();
        using var vm = new MainViewModel(
            new ScewinParser(),
            new ScewinDetector(),
            new ScewinRunner(),
            new SettingsService(),
            new ScewinDownloader(),
            LocalizationService.Instance);

        vm.SetCurrentDump(dump);
        vm.RawSearchQuery = "tCL";
        vm.UpdateRawFilter(immediate: true);

        Assert.Single(vm.FilteredRawTokens);
        Assert.Equal("tCL", vm.FilteredRawTokens[0].Question);
    }
}
