using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SCEWIN_Studio.Models;

public enum MemoryTier
{
    None = 0,
    Tier1Cbs = 1,
    Tier2Msi = 2,
    Tier3Pbs = 3
}

public partial class ScewinToken : ObservableObject
{
    public string Question { get; set; } = string.Empty;
    public string HelpString { get; set; } = string.Empty;
    public string TokenId { get; set; } = string.Empty;
    public string Offset { get; set; } = string.Empty;
    public string Width { get; set; } = string.Empty;
    public List<ScewinOption> Options { get; set; } = new();

    public ScewinOption? OriginalOption { get; set; }

    [ObservableProperty]
    private ScewinOption? _currentOption;

    [ObservableProperty]
    private string? _customNumericValue;

    [ObservableProperty]
    private MemoryTier _memoryTier = MemoryTier.None;

    public bool IsTier1Cbs => MemoryTier == MemoryTier.Tier1Cbs;
    public bool IsTier2Msi => MemoryTier == MemoryTier.Tier2Msi;
    public bool IsTier3Pbs => MemoryTier == MemoryTier.Tier3Pbs;
    public bool HasMemoryTier => MemoryTier != MemoryTier.None;

    public string MemoryTierBadgeText => MemoryTier switch
    {
        MemoryTier.Tier1Cbs => "Основной / AMD CBS",
        MemoryTier.Tier2Msi => "Оверлей MSI BIOS (0 = Auto)",
        MemoryTier.Tier3Pbs => "Служебный дубликат / AMD PBS",
        _ => string.Empty
    };

    public string OriginalNumericValue { get; set; } = string.Empty;
    public bool NumericHasAngleBrackets { get; set; }

    public string Category { get; set; } = "Other";
    public string SubCategory { get; set; } = "General";
    public string SafetyLevel { get; set; } = "Safe"; // Safe, Advanced, Warning

    public string SubCategoryBadgeText => SubCategory switch
    {
        "PboCurve" => "PBO & Curve",
        "PboLimits" => "Лимиты мощности",
        "CpuPowerStates" => "C-States & CPPC",
        "CpuTopology" => "Ядра & SMT",
        "PrimaryTimings" => "Первичные тайминги",
        "SecondaryTimings" => "Вторичные тайминги",
        "TertiaryTimings" => "Третичные тайминги",
        "TerminationsGdm" => "Сопротивления & GDM",
        "Voltages" => "Напряжения памяти",
        "Frequency" => "Частоты & Шина",
        "AspmL1" => "ASPM & L1 Substates",
        "BifurcationSpeed" => "Bifurcation & Скорость",
        "ReBar" => "Re-Size BAR & Above 4G",
        "FanProfiles" => "Вентиляторы",
        "ThermalLimits" => "Температурные лимиты",
        "VrmPower" => "Питание VRM",
        "Network" => "Сетевые адаптеры",
        "UsbThunderbolt" => "USB & Thunderbolt",
        "AudioRgb" => "Звук & Подсветка",
        "BootParams" => "Параметры загрузки",
        "SecurityTpm" => "Безопасность & TPM",
        "StorageRaid" => "Накопители & RAID",
        _ => SubCategory
    };


    public bool HasOptions => Options.Count > 0;

    public bool IsModified
    {
        get
        {
            if (HasOptions)
            {
                if (OriginalOption == null && CurrentOption == null) return false;
                if (OriginalOption == null || CurrentOption == null) return true;
                return !string.Equals(OriginalOption.ValueHex, CurrentOption.ValueHex, StringComparison.OrdinalIgnoreCase);
            }
            return !string.Equals(OriginalNumericValue, CustomNumericValue, StringComparison.OrdinalIgnoreCase);
        }
    }

    public string CurrentDisplayValue
    {
        get
        {
            if (HasOptions)
            {
                return CurrentOption?.DisplayText ?? CurrentOption?.ValueHex ?? "—";
            }
            return CustomNumericValue ?? OriginalNumericValue;
        }
    }

    public string OriginalDisplayValue
    {
        get
        {
            if (HasOptions)
            {
                return OriginalOption?.DisplayText ?? OriginalOption?.ValueHex ?? "—";
            }
            return OriginalNumericValue;
        }
    }

    [ObservableProperty]
    private bool _isDetailsExpanded;

    [RelayCommand]
    public void ToggleDetails()
    {
        IsDetailsExpanded = !IsDetailsExpanded;
    }

    public string DisplayTitle
    {
        get
        {
            var q = System.Text.RegularExpressions.Regex.Replace(Question.Trim(), @"\s+", " ");
            if (q.Equals("PM L1 SS", StringComparison.OrdinalIgnoreCase))
                return "Active State Power Management (ASPM)";
            if (q.Equals("PCIe/GFX Lanes Configuration", StringComparison.OrdinalIgnoreCase))
                return "PCIe Slot Bifurcation";
            return q;
        }
    }

    public bool IsBinaryToggle => HasOptions &&
        ((Options.Count == 2 &&
          Options.Any(o => o.DisplayText.IndexOf("disable", StringComparison.OrdinalIgnoreCase) >= 0 || o.DisplayText.IndexOf("off", StringComparison.OrdinalIgnoreCase) >= 0) &&
          Options.Any(o => o.DisplayText.IndexOf("enable", StringComparison.OrdinalIgnoreCase) >= 0 || o.DisplayText.IndexOf("on", StringComparison.OrdinalIgnoreCase) >= 0))
         ||
         (Question.Contains("c-state", StringComparison.OrdinalIgnoreCase) &&
          Options.Any(o => o.DisplayText.IndexOf("disable", StringComparison.OrdinalIgnoreCase) >= 0)));

    public bool IsChecked
    {
        get
        {
            if (!HasOptions || CurrentOption == null) return false;
            var txt = CurrentOption.DisplayText.ToLowerInvariant();
            if (txt.Contains("disable") || txt.Contains("off")) return false;
            return txt.Contains("enable") || txt.Contains("on") || txt.Contains("auto") || txt.Contains("optimal");
        }
        set
        {
            if (!IsBinaryToggle) return;
            var target = Options.FirstOrDefault(o =>
            {
                var txt = o.DisplayText.ToLowerInvariant();
                return value
                    ? (txt.Contains("enable") || txt.Contains("on") || txt.Contains("auto"))
                    : (txt.Contains("disable") || txt.Contains("off"));
            });
            if (target != null)
            {
                CurrentOption = target;
            }
        }
    }

    public string ToggleStatusText => IsChecked ? "On" : "Off";

    public bool HasHexClockOptions => Options.Any(o => o.DisplayText.EndsWith("h Clk", StringComparison.OrdinalIgnoreCase));

    public string? TimingSubtitle
    {
        get
        {
            if (HasHexClockOptions)
            {
                return "Hex Clk: 10h = 16, 12h = 18, 26h = 38 (значения в тактах)";
            }
            if (IsTier2Msi)
            {
                return "0 = режим Auto (активны регистры CBS). Ввод числа переопределяет CBS.";
            }
            if (IsTier3Pbs)
            {
                return "Служебный дубликат (Auto = наследование параметров)";
            }
            return null;
        }
    }

    public bool HasTimingSubtitle => !string.IsNullOrEmpty(TimingSubtitle);

    public bool HasInfoTooltip => !string.IsNullOrWhiteSpace(InfoTooltipText);

    public string? InfoTooltipText
    {
        get
        {
            if (MemoryTier == MemoryTier.Tier1Cbs)
            {
                return "AMD CBS (AmdSetup): Аппаратные регистры AGESA в шестнадцатеричном формате (10h Clk = 16 тактов, 12h Clk = 18, 26h Clk = 38). Если в оверлее MSI BIOS установлено 0 (Auto), процессор использует именно эти значения напрямую.";
            }
            if (MemoryTier == MemoryTier.Tier2Msi)
            {
                return "MSI Click BIOS OC Engine: OEM-оверлей Setup VarStore (0 = Auto). При значении 0 действуют аппаратные регистры AMD CBS. Если ввести число вручную (например, 16), PEI-драйвер MSI переопределит регистры CBS при старте POST.";
            }
            if (MemoryTier == MemoryTier.Tier3Pbs)
            {
                return "AMD Overclocking PBS: Служебный дубликат меню AMD. По умолчанию находится в [FF]Auto и синхронизируется с первичными настройками.";
            }

            var q = (Question + " " + HelpString).ToLowerInvariant();
            if (q.Contains("bifurcation") || q.Contains("lanes configuration"))
                return "Конфигурация распределения линий PCIe между слотами материнской платы (x16, x8/x8 и т.д.).";
            return null;
        }
    }

    public bool HasInfoBanner => !string.IsNullOrWhiteSpace(InfoBannerText);

    public string? InfoBannerText
    {
        get
        {
            var q = (Question + " " + HelpString).ToLowerInvariant();
            if (q.Contains("bifurcation") || q.Contains("lanes configuration"))
                return "Конфигурация распределения линий PCIe между слотами материнской платы (x16, x8/x8 и т.д.).";
            return null;
        }
    }

    public bool IsZeroAuto
    {
        get
        {
            if (!IsTier2Msi) return false;
            var val = (CustomNumericValue ?? OriginalNumericValue ?? string.Empty).Replace("<", "").Replace(">", "").Trim();
            return val == "0" || string.IsNullOrEmpty(val);
        }
    }

    public bool IsOptimal
    {
        get
        {
            var q = (Question + " " + HelpString).ToLowerInvariant();
            var val = CurrentDisplayValue.ToLowerInvariant();
            if (q.Contains("aspm") && (val.Contains("disable") || val.Contains("off") || val.Contains("l0 permanent"))) return true;
            if (q.Contains("bar") && (val.Contains("enable") || val.Contains("on"))) return true;
            if (q.Contains("c-state") && (val.Contains("disable") || val.Contains("off"))) return true;
            if (q.Contains("bifurcation") && (val.Contains("auto") || val.Contains("x16"))) return true;
            if (q.Contains("curve") && val.Contains("negative")) return true;
            return false;
        }
    }

    public string IconGlyph
    {
        get
        {
            var q = (Question + " " + HelpString).ToLowerInvariant();
            if (SubCategory == "CpuPowerStates" || q.Contains("c-state") || q.Contains("sleep") || q.Contains("power saving") || q.Contains("deep sleep"))
                return "\uEC46"; // Crescent Moon / Sleep
            if (SubCategory == "PboCurve" || SubCategory == "PboLimits" || q.Contains("curve") || q.Contains("pbo") || q.Contains("overclock") || q.Contains("voltage") || q.Contains("frequency") || q.Contains("ratio"))
                return "\uE9E9"; // Sliders / Tuning
            if (SubCategory == "AspmL1" || SubCategory == "BifurcationSpeed" || SubCategory == "ReBar" || q.Contains("bifurcation") || q.Contains("lane") || q.Contains("slot") || q.Contains("aspm") || q.Contains("pcie") || q.Contains("pci") || q.Contains("express") || q.Contains("link width"))
                return "\uE765"; // PCIe / Expansion Board
            if (Category == "Memory" || HasMemoryTier || SubCategory == "PrimaryTimings" || SubCategory == "SecondaryTimings" || SubCategory == "TertiaryTimings" || SubCategory == "TerminationsGdm" || SubCategory == "Voltages" || SubCategory == "Frequency" || q.Contains("memory") || q.Contains("dram") || q.Contains("timing") || q.Contains("fclk") || q.Contains("mclk") || q.Contains("tcl") || q.Contains("trcd") || q.Contains("trp") || q.Contains("tras") || q.Contains("cas"))
                return "\uE950"; // Memory / RAM Chip
            if (q.Contains("bar") || q.Contains("re-size") || q.Contains("above 4g"))
                return "\uE765"; // Memory / BAR
            if (SubCategory == "FanProfiles" || SubCategory == "ThermalLimits" || SubCategory == "VrmPower" || q.Contains("fan") || q.Contains("thermal"))
                return "\uE945"; // Thermal / Power
            if (SubCategory == "Network" || q.Contains("lan") || q.Contains("wifi") || q.Contains("ethernet"))
                return "\uE839"; // Network
            if (SubCategory == "UsbThunderbolt" || q.Contains("usb") || q.Contains("thunderbolt"))
                return "\uE88E"; // USB
            if (SubCategory == "AudioRgb" || q.Contains("audio") || q.Contains("rgb"))
                return "\uE74F"; // Audio
            if (SubCategory == "BootParams" || SubCategory == "SecurityTpm" || SubCategory == "StorageRaid" || q.Contains("boot") || q.Contains("tpm"))
                return "\uE72E"; // Lock / Security
            if (q.Contains("cpu") || q.Contains("core") || q.Contains("processor") || q.Contains("thread"))
                return "\uE950"; // CPU Chip
            return "\uE71D"; // General setting / slider
        }
    }

    public bool IsNumericStepper => !HasOptions && int.TryParse(CurrentDisplayValue?.Replace("<", "").Replace(">", "").Trim(), out _);

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    public void IncrementNumeric()
    {
        var cur = CurrentDisplayValue?.Replace("<", "").Replace(">", "").Trim() ?? "0";
        if (int.TryParse(cur, out int val))
        {
            CustomNumericValue = (val + 1).ToString();
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    public void DecrementNumeric()
    {
        var cur = CurrentDisplayValue?.Replace("<", "").Replace(">", "").Trim() ?? "0";
        if (int.TryParse(cur, out int val))
        {
            if (val > 0)
            {
                CustomNumericValue = (val - 1).ToString();
            }
            else
            {
                CustomNumericValue = "0";
            }
        }
    }

    partial void OnCurrentOptionChanged(ScewinOption? value)
    {
        OnPropertyChanged(nameof(IsModified));
        OnPropertyChanged(nameof(CurrentDisplayValue));
        OnPropertyChanged(nameof(IsChecked));
        OnPropertyChanged(nameof(ToggleStatusText));
        OnPropertyChanged(nameof(IsOptimal));
    }

    partial void OnCustomNumericValueChanged(string? value)
    {
        OnPropertyChanged(nameof(IsModified));
        OnPropertyChanged(nameof(CurrentDisplayValue));
        OnPropertyChanged(nameof(IsOptimal));
        OnPropertyChanged(nameof(IsNumericStepper));
        OnPropertyChanged(nameof(IsZeroAuto));
    }

    partial void OnMemoryTierChanged(MemoryTier value)
    {
        OnPropertyChanged(nameof(IsTier1Cbs));
        OnPropertyChanged(nameof(IsTier2Msi));
        OnPropertyChanged(nameof(IsTier3Pbs));
        OnPropertyChanged(nameof(HasMemoryTier));
        OnPropertyChanged(nameof(MemoryTierBadgeText));
        OnPropertyChanged(nameof(HasInfoTooltip));
        OnPropertyChanged(nameof(InfoTooltipText));
        OnPropertyChanged(nameof(TimingSubtitle));
        OnPropertyChanged(nameof(HasTimingSubtitle));
        OnPropertyChanged(nameof(IsZeroAuto));
        OnPropertyChanged(nameof(IconGlyph));
    }

    public void Reset()
    {
        CurrentOption = OriginalOption;
        CustomNumericValue = OriginalNumericValue;
    }
}
