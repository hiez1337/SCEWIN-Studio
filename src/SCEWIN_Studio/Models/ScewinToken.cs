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

    private string? _searchableLower;
    public string SearchableLower => _searchableLower ??= $"{DisplayTitle} {Question} {HelpString}".ToLowerInvariant();

    private string? _displayTitle;
    public string DisplayTitle
    {
        get
        {
            if (_displayTitle == null)
            {
                var q = Question.Trim();
                if (q.Equals("PM L1 SS", StringComparison.OrdinalIgnoreCase))
                    _displayTitle = "Active State Power Management (ASPM)";
                else if (q.Equals("PCIe/GFX Lanes Configuration", StringComparison.OrdinalIgnoreCase))
                    _displayTitle = "PCIe Slot Bifurcation";
                else if (q.IndexOf("  ", StringComparison.Ordinal) >= 0)
                    _displayTitle = System.Text.RegularExpressions.Regex.Replace(q, @"\s+", " ");
                else
                    _displayTitle = q;
            }
            return _displayTitle;
        }
    }

    private bool? _isBinaryToggle;
    public bool IsBinaryToggle
    {
        get
        {
            if (!_isBinaryToggle.HasValue)
            {
                _isBinaryToggle = HasOptions &&
                    ((Options.Count == 2 &&
                      Options.Any(o => o.DisplayText.IndexOf("disable", StringComparison.OrdinalIgnoreCase) >= 0 || o.DisplayText.IndexOf("off", StringComparison.OrdinalIgnoreCase) >= 0) &&
                      Options.Any(o => o.DisplayText.IndexOf("enable", StringComparison.OrdinalIgnoreCase) >= 0 || o.DisplayText.IndexOf("on", StringComparison.OrdinalIgnoreCase) >= 0))
                     ||
                     (Question.Contains("c-state", StringComparison.OrdinalIgnoreCase) &&
                      Options.Any(o => o.DisplayText.IndexOf("disable", StringComparison.OrdinalIgnoreCase) >= 0)));
            }
            return _isBinaryToggle.Value;
        }
    }

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
            if (!IsBinaryToggle)
            {
                OnPropertyChanged(nameof(IsChecked));
                return;
            }
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
            else
            {
                OnPropertyChanged(nameof(IsChecked));
            }
        }
    }

    public string ToggleStatusText => IsChecked ? "On" : "Off";

    private bool? _hasHexClockOptions;
    public bool HasHexClockOptions
    {
        get
        {
            if (!_hasHexClockOptions.HasValue)
            {
                _hasHexClockOptions = Options.Any(o => o.DisplayText.EndsWith("h Clk", StringComparison.OrdinalIgnoreCase));
            }
            return _hasHexClockOptions.Value;
        }
    }

    private string? _timingSubtitle;
    private bool _timingSubtitleComputed;
    public string? TimingSubtitle
    {
        get
        {
            if (!_timingSubtitleComputed)
            {
                _timingSubtitle = ComputeTimingSubtitle();
                _timingSubtitleComputed = true;
            }
            return _timingSubtitle;
        }
    }

    private string? ComputeTimingSubtitle()
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

    public bool HasTimingSubtitle => !string.IsNullOrEmpty(TimingSubtitle);

    private string? _infoTooltipText;
    private bool _infoTooltipComputed;
    public string? InfoTooltipText
    {
        get
        {
            if (!_infoTooltipComputed)
            {
                _infoTooltipText = ComputeInfoTooltipText();
                _infoTooltipComputed = true;
            }
            return _infoTooltipText;
        }
    }

    public bool HasInfoTooltip => !string.IsNullOrWhiteSpace(InfoTooltipText);

    private string? ComputeInfoTooltipText()
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

        var q = SearchableLower;
        if (q.Contains("bifurcation") || q.Contains("lanes configuration"))
            return "Конфигурация распределения линий PCIe между слотами материнской платы (x16, x8/x8 и т.д.).";
        return null;
    }

    private string? _infoBannerText;
    private bool _infoBannerComputed;
    public string? InfoBannerText
    {
        get
        {
            if (!_infoBannerComputed)
            {
                _infoBannerText = ComputeInfoBannerText();
                _infoBannerComputed = true;
            }
            return _infoBannerText;
        }
    }

    public bool HasInfoBanner => !string.IsNullOrWhiteSpace(InfoBannerText);

    private string? ComputeInfoBannerText()
    {
        var q = SearchableLower;
        if (q.Contains("bifurcation") || q.Contains("lanes configuration"))
            return "Конфигурация распределения линий PCIe между слотами материнской платы (x16, x8/x8 и т.д.).";
        return null;
    }

    private bool? _isZeroAuto;
    public bool IsZeroAuto
    {
        get
        {
            if (!_isZeroAuto.HasValue)
            {
                if (!IsTier2Msi)
                {
                    _isZeroAuto = false;
                }
                else
                {
                    var val = (CustomNumericValue ?? OriginalNumericValue ?? string.Empty).Replace("<", "").Replace(">", "").Trim();
                    _isZeroAuto = val == "0" || string.IsNullOrEmpty(val);
                }
            }
            return _isZeroAuto.Value;
        }
    }

    private bool? _isOptimal;
    public bool IsOptimal
    {
        get
        {
            if (!_isOptimal.HasValue)
            {
                var q = SearchableLower;
                var val = CurrentDisplayValue.ToLowerInvariant();
                if (q.Contains("aspm") && (val.Contains("disable") || val.Contains("off") || val.Contains("l0 permanent"))) _isOptimal = true;
                else if (q.Contains("bar") && (val.Contains("enable") || val.Contains("on"))) _isOptimal = true;
                else if (q.Contains("c-state") && (val.Contains("disable") || val.Contains("off"))) _isOptimal = true;
                else if (q.Contains("bifurcation") && (val.Contains("auto") || val.Contains("x16"))) _isOptimal = true;
                else if (q.Contains("curve") && val.Contains("negative")) _isOptimal = true;
                else _isOptimal = false;
            }
            return _isOptimal.Value;
        }
    }

    private string? _iconGlyph;
    public string IconGlyph
    {
        get
        {
            if (_iconGlyph == null)
            {
                _iconGlyph = ComputeIconGlyph();
            }
            return _iconGlyph;
        }
    }

    private string ComputeIconGlyph()
    {
        var q = SearchableLower;
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

    private bool? _isNumericStepper;
    public bool IsNumericStepper
    {
        get
        {
            if (!_isNumericStepper.HasValue)
            {
                if (HasOptions)
                {
                    _isNumericStepper = false;
                }
                else
                {
                    var val = CurrentDisplayValue;
                    if (val == null)
                    {
                        _isNumericStepper = false;
                    }
                    else
                    {
                        var trimmed = val.Replace("<", "").Replace(">", "").Trim();
                        if (int.TryParse(trimmed, out _))
                        {
                            _isNumericStepper = true;
                        }
                        else if (trimmed.EndsWith("h", StringComparison.OrdinalIgnoreCase) &&
                                 int.TryParse(trimmed.Substring(0, trimmed.Length - 1), System.Globalization.NumberStyles.HexNumber, null, out _))
                        {
                            _isNumericStepper = true;
                        }
                        else
                        {
                            _isNumericStepper = false;
                        }
                    }
                }
            }
            return _isNumericStepper.Value;
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    public void IncrementNumeric()
    {
        var cur = CurrentDisplayValue?.Replace("<", "").Replace(">", "").Trim() ?? "0";
        if (int.TryParse(cur, out int val))
        {
            CustomNumericValue = (val + 1).ToString();
        }
        else if (cur.EndsWith("h", StringComparison.OrdinalIgnoreCase) &&
                 int.TryParse(cur.Substring(0, cur.Length - 1), System.Globalization.NumberStyles.HexNumber, null, out int hexVal))
        {
            CustomNumericValue = $"{hexVal + 1:X}h";
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    public void DecrementNumeric()
    {
        var cur = CurrentDisplayValue?.Replace("<", "").Replace(">", "").Trim() ?? "0";
        if (int.TryParse(cur, out int val))
        {
            CustomNumericValue = val > 0 ? (val - 1).ToString() : "0";
        }
        else if (cur.EndsWith("h", StringComparison.OrdinalIgnoreCase) &&
                 int.TryParse(cur.Substring(0, cur.Length - 1), System.Globalization.NumberStyles.HexNumber, null, out int hexVal))
        {
            CustomNumericValue = hexVal > 0 ? $"{hexVal - 1:X}h" : "0h";
        }
    }

    partial void OnCurrentOptionChanged(ScewinOption? value)
    {
        _isOptimal = null;
        OnPropertyChanged(nameof(IsModified));
        OnPropertyChanged(nameof(CurrentDisplayValue));
        OnPropertyChanged(nameof(IsChecked));
        OnPropertyChanged(nameof(ToggleStatusText));
        OnPropertyChanged(nameof(IsOptimal));
    }

    partial void OnCustomNumericValueChanged(string? value)
    {
        _isOptimal = null;
        _isZeroAuto = null;
        _isNumericStepper = null;
        OnPropertyChanged(nameof(IsModified));
        OnPropertyChanged(nameof(CurrentDisplayValue));
        OnPropertyChanged(nameof(IsOptimal));
        OnPropertyChanged(nameof(IsNumericStepper));
        OnPropertyChanged(nameof(IsZeroAuto));
    }

    partial void OnMemoryTierChanged(MemoryTier value)
    {
        _iconGlyph = null;
        _infoTooltipComputed = false;
        _infoBannerComputed = false;
        _timingSubtitleComputed = false;
        _isZeroAuto = null;
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
