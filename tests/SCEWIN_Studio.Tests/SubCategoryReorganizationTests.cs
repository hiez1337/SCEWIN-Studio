using System;
using System.Linq;
using System.Threading.Tasks;
using SCEWIN_Studio.Models;
using SCEWIN_Studio.Services;
using SCEWIN_Studio.ViewModels;
using Xunit;

namespace SCEWIN_Studio.Tests;

public class SubCategoryReorganizationTests
{
    private readonly ScewinParser _parser = new();

    [Theory]
    [InlineData("Curve Optimizer", "Configure core voltage offsets", "Overclocking", "PboCurve")]
    [InlineData("Precision Boost Overdrive", "PBO settings", "Overclocking", "PboCurve")]
    [InlineData("CPU Boost Clock Override", "Max boost override", "Overclocking", "PboCurve")]
    [InlineData("PPT Limit", "Package power tracking limit", "Overclocking", "PboLimits")]
    [InlineData("TDC Limit", "Thermal design current limit", "Overclocking", "PboLimits")]
    [InlineData("EDC Limit", "Electrical design current limit", "Overclocking", "PboLimits")]
    [InlineData("Global C-state Control", "Enable CPU C-States", "CpuPower", "CpuPowerStates")]
    [InlineData("DF C-States", "Data Fabric C-States", "CpuPower", "CpuPowerStates")]
    [InlineData("CPPC Preferred Cores", "Collaborative processor performance control", "CpuPower", "CpuPowerStates")]
    [InlineData("SMT Control", "Simultaneous Multi-Threading", "CpuPower", "CpuTopology")]
    [InlineData("CCD Control", "Enable or disable second CCD", "CpuPower", "CpuTopology")]
    [InlineData("Downcore configuration", "Active processor cores", "CpuPower", "CpuTopology")]
    public void CpuAndOverclocking_SubCategories_ClassifiedCorrectly(string question, string help, string expectedCat, string expectedSubCat)
    {
        var cat = ScewinParser.DetermineCategory(question, help);
        var subCat = ScewinParser.DetermineSubCategory(question, help, cat);

        Assert.Equal(expectedCat, cat);
        Assert.Equal(expectedSubCat, subCat);
    }

    [Theory]
    [InlineData("tCL", "CAS Latency", "Memory", "PrimaryTimings")]
    [InlineData("tRCDRD", "RAS# to CAS# Read Delay", "Memory", "PrimaryTimings")]
    [InlineData("tRCDWR", "RAS# to CAS# Write Delay", "Memory", "PrimaryTimings")]
    [InlineData("tRP", "Row Precharge Time", "Memory", "PrimaryTimings")]
    [InlineData("tRAS", "Row Active Time", "Memory", "PrimaryTimings")]
    [InlineData("tRC", "Row Cycle Time", "Memory", "PrimaryTimings")]
    [InlineData("tFAW", "Four Activate Window", "Memory", "SecondaryTimings")]
    [InlineData("tRRDS", "Row to Row Delay Short", "Memory", "SecondaryTimings")]
    [InlineData("tRRDL", "Row to Row Delay Long", "Memory", "SecondaryTimings")]
    [InlineData("tWTRS", "Write to Read Delay Short", "Memory", "SecondaryTimings")]
    [InlineData("tWTRL", "Write to Read Delay Long", "Memory", "SecondaryTimings")]
    [InlineData("tWR", "Write Recovery Time", "Memory", "SecondaryTimings")]
    [InlineData("tCWL", "CAS Write Latency", "Memory", "SecondaryTimings")]
    [InlineData("tRDRD_SC", "Read to Read Delay", "Memory", "TertiaryTimings")]
    [InlineData("tWRWR_SD", "Write to Write Delay", "Memory", "TertiaryTimings")]
    [InlineData("tRDWR", "Read to Write Delay", "Memory", "TertiaryTimings")]
    [InlineData("tWRRD", "Write to Read Delay", "Memory", "TertiaryTimings")]
    [InlineData("ProcODT", "Processor On-Die Termination", "Memory", "TerminationsGdm")]
    [InlineData("RttNom", "RTT Nominal", "Memory", "TerminationsGdm")]
    [InlineData("RttWr", "RTT Write", "Memory", "TerminationsGdm")]
    [InlineData("RttPark", "RTT Park", "Memory", "TerminationsGdm")]
    [InlineData("Gear Down Mode", "GDM enable or disable", "Memory", "TerminationsGdm")]
    [InlineData("Power Down Enable", "DRAM power down", "Memory", "TerminationsGdm")]
    [InlineData("tRFC", "Refresh Cycle Time", "Memory", "TerminationsGdm")]
    [InlineData("tREFI", "Refresh Interval", "Memory", "TerminationsGdm")]
    [InlineData("DRAM Voltage", "Memory VDD voltage", "Memory", "Voltages")]
    [InlineData("CPU VDDIO", "Memory controller VDDIO", "Memory", "Voltages")]
    [InlineData("CPU VDDP", "PHY VDDP voltage", "Memory", "Voltages")]
    [InlineData("SoC Voltage", "VDDCR_SOC voltage control", "Memory", "Voltages")]
    [InlineData("FCLK Frequency", "Infinity Fabric Clock", "Memory", "Frequency")]
    [InlineData("UCLK DIV1 MODE", "UCLK to MCLK divider 1:1 or 1:2", "Memory", "Frequency")]
    [InlineData("Adjusted DRAM Frequency Value", "Target memory clock", "Memory", "Frequency")]
    public void Memory_SubCategories_ClassifiedCorrectly(string question, string help, string expectedCat, string expectedSubCat)
    {
        var cat = ScewinParser.DetermineCategory(question, help);
        var subCat = ScewinParser.DetermineSubCategory(question, help, cat);

        Assert.Equal(expectedCat, cat);
        Assert.Equal(expectedSubCat, subCat);
    }

    [Theory]
    [InlineData("Active State Power Management (ASPM)", "Control ASPM", "ASPM", "AspmL1")]
    [InlineData("PM L1 SS", "PCIe L1 Substates", "ASPM", "AspmL1")]
    [InlineData("PCIe Slot Bifurcation", "Lanes configuration", "ASPM", "BifurcationSpeed")]
    [InlineData("PCIe x16 Link Speed", "Gen1/Gen2/Gen3/Gen4/Gen5", "ASPM", "BifurcationSpeed")]
    [InlineData("Re-Size BAR Support", "Smart Access Memory", "ASPM", "ReBar")]
    [InlineData("Above 4G Decoding", "Decode 64-bit address space", "ASPM", "ReBar")]
    public void Pcie_SubCategories_ClassifiedCorrectly(string question, string help, string expectedCat, string expectedSubCat)
    {
        var cat = ScewinParser.DetermineCategory(question, help);
        var subCat = ScewinParser.DetermineSubCategory(question, help, cat);

        Assert.Equal(expectedCat, cat);
        Assert.Equal(expectedSubCat, subCat);
    }

    [Theory]
    [InlineData("CPU Fan Control Mode", "PWM or DC fan mode", "CoolingPower", "FanProfiles")]
    [InlineData("System Fan 1 Step Up Time", "Fan speed smoothing", "CoolingPower", "FanProfiles")]
    [InlineData("Thermal Throttle Limit (TjMax)", "Maximum CPU operating temperature", "CoolingPower", "ThermalLimits")]
    [InlineData("VRM Switching Frequency", "MOSFET PWM switching frequency", "CoolingPower", "VrmPower")]
    public void CoolingAndPower_SubCategories_ClassifiedCorrectly(string question, string help, string expectedCat, string expectedSubCat)
    {
        var cat = ScewinParser.DetermineCategory(question, help);
        var subCat = ScewinParser.DetermineSubCategory(question, help, cat);

        Assert.Equal(expectedCat, cat);
        Assert.Equal(expectedSubCat, subCat);
    }

    [Theory]
    [InlineData("Onboard LAN Controller", "Realtek 2.5G LAN", "Peripherals", "Network")]
    [InlineData("Wireless LAN Recovery", "WLAN recovery support", "Peripherals", "Network")]
    [InlineData("Bluetooth PLDR Support", "BT controller", "Peripherals", "Network")]
    [InlineData("USB Controller & XHCI Hand-off", "USB 3.0 XHCI support", "Peripherals", "UsbThunderbolt")]
    [InlineData("Thunderbolt Support", "Intel Thunderbolt host chipset", "Peripherals", "UsbThunderbolt")]
    [InlineData("HD Audio Controller", "Azalia onboard audio", "Peripherals", "AudioRgb")]
    [InlineData("Motherboard LED / RGB Control", "Mystic Light onboard LEDs", "Peripherals", "AudioRgb")]
    public void Peripherals_SubCategories_ClassifiedCorrectly(string question, string help, string expectedCat, string expectedSubCat)
    {
        var cat = ScewinParser.DetermineCategory(question, help);
        var subCat = ScewinParser.DetermineSubCategory(question, help, cat);

        Assert.Equal(expectedCat, cat);
        Assert.Equal(expectedSubCat, subCat);
    }

    [Theory]
    [InlineData("Fast Boot", "Bypass hardware initialization during POST", "BootSecurity", "BootParams")]
    [InlineData("Boot Mode Select", "UEFI only or CSM legacy boot", "BootSecurity", "BootParams")]
    [InlineData("POST Delay Time", "Delay in seconds for keyboard input", "BootSecurity", "BootParams")]
    [InlineData("AMD fTPM switch", "Enable firmware TPM 2.0", "BootSecurity", "SecurityTpm")]
    [InlineData("Secure Boot", "Standard or Custom Secure Boot", "BootSecurity", "SecurityTpm")]
    [InlineData("SATA Mode", "AHCI or RAID mode", "BootSecurity", "StorageRaid")]
    [InlineData("NVMe RAID Mode", "Enable AMD RAIDXpert2 for NVMe", "BootSecurity", "StorageRaid")]
    public void BootAndSecurity_SubCategories_ClassifiedCorrectly(string question, string help, string expectedCat, string expectedSubCat)
    {
        var cat = ScewinParser.DetermineCategory(question, help);
        var subCat = ScewinParser.DetermineSubCategory(question, help, cat);

        Assert.Equal(expectedCat, cat);
        Assert.Equal(expectedSubCat, subCat);
    }

    [Fact]
    public void AntiCrossContamination_MemoryTimingsAndVoltages_MustNotLeakIntoOverclocking()
    {
        // 1. Trcdrd inside an Overclocking form must be classified as Memory, NOT Overclocking
        var timingCat = ScewinParser.DetermineCategory("Trcdrd", "DRAM Timing Configuration inside Overclocking form");
        Assert.Equal("Memory", timingCat);
        Assert.NotEqual("Overclocking", timingCat);

        // 2. VDDIO Voltage Control inside Overclocking form must be Memory, NOT Overclocking
        var vddioCat = ScewinParser.DetermineCategory("VDDIO Voltage Control", "DRAM Memory controller voltage");
        Assert.Equal("Memory", vddioCat);
        Assert.NotEqual("Overclocking", vddioCat);

        // 3. Adjusted DRAM Frequency must be Memory, NOT Overclocking
        var dramFreqCat = ScewinParser.DetermineCategory("Adjusted DRAM Frequency Value", "Memory clock in Overclocking menu");
        Assert.Equal("Memory", dramFreqCat);
        Assert.NotEqual("Overclocking", dramFreqCat);
    }

    [Fact]
    public void AntiCrossContamination_ThunderboltAndUsb_MustNotLeakIntoFansOrPower()
    {
        // Thunderbolt Wake Up Command contains "Wake Up" and "Command" — must NOT leak into Fans/Power
        var tbCat = ScewinParser.DetermineCategory("Thunderbolt Wake Up Command", "Send low power wake up command to host controller");
        Assert.Equal("Peripherals", tbCat);
        Assert.NotEqual("CoolingPower", tbCat);
        Assert.NotEqual("CpuPower", tbCat);

        var tbSubCat = ScewinParser.DetermineSubCategory("Thunderbolt Wake Up Command", "Host controller wake", tbCat);
        Assert.Equal("UsbThunderbolt", tbSubCat);
    }

    [Fact]
    public void AntiCrossContamination_FastBoot_MustNotLeakIntoCpuOrFrequency()
    {
        // Fast Boot contains "Fast" and is often under generic menus — must be BootSecurity
        var fbCat = ScewinParser.DetermineCategory("Fast Boot", "Accelerate system boot time");
        Assert.Equal("BootSecurity", fbCat);
        Assert.NotEqual("CpuPower", fbCat);
        Assert.NotEqual("Overclocking", fbCat);

        var fbSubCat = ScewinParser.DetermineSubCategory("Fast Boot", "Boot option", fbCat);
        Assert.Equal("BootParams", fbSubCat);
    }

    [Fact]
    public async Task ViewModel_SubCategoryFilters_FilterTokensWithoutUiFreeze()
    {
        var dump = new ScewinDump();
        dump.Tokens.Add(new ScewinToken { Question = "Curve Optimizer", TokenId = "01", Category = "Overclocking", SubCategory = "PboCurve" });
        dump.Tokens.Add(new ScewinToken { Question = "PPT Limit", TokenId = "02", Category = "Overclocking", SubCategory = "PboLimits" });
        dump.Tokens.Add(new ScewinToken { Question = "Global C-state Control", TokenId = "03", Category = "CpuPower", SubCategory = "CpuPowerStates" });
        dump.Tokens.Add(new ScewinToken { Question = "SMT Control", TokenId = "04", Category = "CpuPower", SubCategory = "CpuTopology" });
        dump.Tokens.Add(new ScewinToken { Question = "tCL", TokenId = "2A28", Category = "Memory", SubCategory = "PrimaryTimings", MemoryTier = MemoryTier.Tier2Msi });
        dump.Tokens.Add(new ScewinToken { Question = "ProcODT", TokenId = "06", Category = "Memory", SubCategory = "TerminationsGdm" });
        dump.Tokens.Add(new ScewinToken { Question = "DRAM Voltage", TokenId = "07", Category = "Memory", SubCategory = "Voltages" });
        dump.Tokens.Add(new ScewinToken { Question = "PM L1 SS", TokenId = "08", Category = "ASPM", SubCategory = "AspmL1" });
        dump.Tokens.Add(new ScewinToken { Question = "Re-Size BAR Support", TokenId = "09", Category = "ASPM", SubCategory = "ReBar" });

        var vm = new MainViewModel(
            _parser,
            new ScewinDetector(),
            new ScewinRunner(),
            new SettingsService(),
            new ScewinDownloader(),
            LocalizationService.Instance);

        vm.SetCurrentDump(dump);

        // 1. Overclocking View SubCategory filter
        Assert.Equal(2, vm.FilteredOverclockingTokens.Count); // "All" default
        vm.SetOverclockingSubCategory("PboCurve");
        Assert.Single(vm.FilteredOverclockingTokens);
        Assert.Equal("Curve Optimizer", vm.FilteredOverclockingTokens[0].Question);

        // Overclocking filter allows switching to C-States
        vm.SetOverclockingSubCategory("CpuPowerStates");
        Assert.Single(vm.FilteredOverclockingTokens);
        Assert.Equal("Global C-state Control", vm.FilteredOverclockingTokens[0].Question);

        // Reset to All
        vm.SetOverclockingSubCategory("All");
        Assert.Equal(2, vm.FilteredOverclockingTokens.Count);

        // 2. PCIe View SubCategory filter
        Assert.Equal(2, vm.FilteredAspmTokens.Count);
        vm.SetPcieSubCategory("ReBar");
        Assert.Single(vm.FilteredAspmTokens);
        Assert.Equal("Re-Size BAR Support", vm.FilteredAspmTokens[0].Question);

        // 3. Memory View SubCategory filter
        Assert.True(vm.HasFilteredMsiOverlayTokens);
        vm.SetMemorySubCategory("PrimaryTimings");
        Assert.True(vm.HasFilteredMsiOverlayTokens);
        Assert.Equal("tCL", vm.FilteredMsiOverlayMemoryTokens[0].Question);

        vm.SetMemorySubCategory("Voltages");
        Assert.False(vm.HasFilteredMsiOverlayTokens); // tCL is not a voltage
        Assert.Single(vm.FilteredGeneralMemoryTokens); // DRAM Voltage is in GeneralMemoryTokens
        Assert.Equal("DRAM Voltage", vm.FilteredGeneralMemoryTokens[0].Question);

        // 4. RawCategoryFilter supports both Category and SubCategory
        vm.SearchDebounceDelayMs = 0;
        vm.RawCategoryFilter = "Voltages";
        if (vm.CurrentSearchTask != null) await vm.CurrentSearchTask;
        Assert.Single(vm.FilteredRawTokens);
        Assert.Equal("DRAM Voltage", vm.FilteredRawTokens[0].Question);

        vm.RawCategoryFilter = "PboLimits";
        if (vm.CurrentSearchTask != null) await vm.CurrentSearchTask;
        Assert.Single(vm.FilteredRawTokens);
        Assert.Equal("PPT Limit", vm.FilteredRawTokens[0].Question);

        vm.RawCategoryFilter = "All";
        if (vm.CurrentSearchTask != null) await vm.CurrentSearchTask;
        Assert.Equal(9, vm.FilteredRawTokens.Count);
    }
}
