using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SCEWIN_Studio.Collections;
using SCEWIN_Studio.Models;
using SCEWIN_Studio.Services;

namespace SCEWIN_Studio.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IScewinParser _parser;
    private readonly IScewinDetector _detector;
    private readonly IScewinRunner _runner;
    private readonly ISettingsService _settingsService;
    private readonly IScewinDownloader _downloader;
    private readonly IDumpComparer _dumpComparer;

    public ILocalizationService L10n { get; }

    [ObservableProperty]
    private ScewinDump? _currentDump;

    [ObservableProperty]
    private string _scewinPath = string.Empty;

    [ObservableProperty]
    private bool _isScewinReady;

    [ObservableProperty]
    private bool _isDriversReady;

    [ObservableProperty]
    private string _scewinDetails = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _loadingMessage = string.Empty;

    [ObservableProperty]
    private string _currentNavView = "Dashboard";

    [ObservableProperty]
    private string _motherboardSummary = "Системная плата (UEFI BIOS)";

    [ObservableProperty]
    private string _navSearchQuery = string.Empty;

    public bool IsDumpLoaded => CurrentDump != null;
    public bool IsAdmin => _runner.IsAdministrator();

    public ObservableCollection<ScewinDiffItem> PendingDiffs { get; } = new();

    public int ModifiedCount => PendingDiffs.Count;
    public bool HasModifiedItems => PendingDiffs.Count > 0;

    // Dual Dump Comparison properties
    [ObservableProperty]
    private ScewinDump? _comparisonDumpA;

    [ObservableProperty]
    private string _comparisonDumpAPath = string.Empty;

    [ObservableProperty]
    private string _comparisonDumpAInfo = "Дамп не выбран";

    [ObservableProperty]
    private ScewinDump? _comparisonDumpB;

    [ObservableProperty]
    private string _comparisonDumpBPath = string.Empty;

    [ObservableProperty]
    private string _comparisonDumpBInfo = "Дамп не выбран";

    [ObservableProperty]
    private string _comparisonFilter = "All"; // All, Different, Equal, OnlyA, OnlyB

    [ObservableProperty]
    private string _comparisonSearchQuery = string.Empty;

    [ObservableProperty]
    private int _totalComparedCount;

    [ObservableProperty]
    private int _differentCount;

    [ObservableProperty]
    private int _equalCount;

    [ObservableProperty]
    private int _onlyInACount;

    [ObservableProperty]
    private int _onlyInBCount;

    public bool HasComparisonResults => TotalComparedCount > 0;
    public bool CanRunComparison => ComparisonDumpA != null && ComparisonDumpB != null;

    public ObservableRangeCollection<DumpComparisonItem> AllComparisonItems { get; } = new();
    public ObservableRangeCollection<DumpComparisonItem> FilteredComparisonItems { get; } = new();

    partial void OnComparisonDumpAChanged(ScewinDump? value)
    {
        UpdateComparisonDumpAInfo();
        OnPropertyChanged(nameof(CanRunComparison));
    }

    partial void OnComparisonDumpBChanged(ScewinDump? value)
    {
        UpdateComparisonDumpBInfo();
        OnPropertyChanged(nameof(CanRunComparison));
    }

    partial void OnComparisonFilterChanged(string value) => UpdateComparisonFilter(immediate: true);
    partial void OnComparisonSearchQueryChanged(string value) => TriggerComparisonFilterDebounced();

    partial void OnCurrentDumpChanged(ScewinDump? value)
    {
        OnPropertyChanged(nameof(IsDumpLoaded));
        OnPropertyChanged(nameof(StatusHeader));
        UpdateMotherboardSummary();
    }

    partial void OnNavSearchQueryChanged(string value)
    {
        RawSearchQuery = value;
    }

    // Filtered collections for sections
    public ObservableCollection<ScewinToken> AspmTokens { get; } = new();
    public ObservableCollection<ScewinToken> OverclockingTokens { get; } = new();
    public ObservableCollection<ScewinToken> MemoryTokens { get; } = new();
    public ObservableCollection<ScewinToken> PrimaryCbsMemoryTokens { get; } = new();
    public ObservableCollection<ScewinToken> MsiOverlayMemoryTokens { get; } = new();
    public ObservableCollection<ScewinToken> PbsDuplicateMemoryTokens { get; } = new();
    public ObservableCollection<ScewinToken> GeneralMemoryTokens { get; } = new();
    public ObservableCollection<ScewinToken> CpuTokens { get; } = new();
    public ObservableCollection<ScewinToken> AllTokens { get; } = new();

    public bool HasPrimaryCbsTokens => PrimaryCbsMemoryTokens.Count > 0;
    public bool HasMsiOverlayTokens => MsiOverlayMemoryTokens.Count > 0;
    public bool HasPbsDuplicateTokens => PbsDuplicateMemoryTokens.Count > 0;
    public bool HasGeneralMemoryTokens => GeneralMemoryTokens.Count > 0;

    // SubCategory filters and filtered collections for views
    [ObservableProperty]
    private string _overclockingSubCategory = "All";

    [ObservableProperty]
    private string _cpuPowerSubCategory = "All";

    [ObservableProperty]
    private string _pcieSubCategory = "All";

    [ObservableProperty]
    private string _memorySubCategory = "All";

    public ObservableRangeCollection<ScewinToken> FilteredOverclockingTokens { get; } = new();
    public ObservableRangeCollection<ScewinToken> FilteredCpuTokens { get; } = new();
    public ObservableRangeCollection<ScewinToken> FilteredAspmTokens { get; } = new();
    public ObservableRangeCollection<ScewinToken> FilteredPrimaryCbsMemoryTokens { get; } = new();
    public ObservableRangeCollection<ScewinToken> FilteredMsiOverlayMemoryTokens { get; } = new();
    public ObservableRangeCollection<ScewinToken> FilteredPbsDuplicateMemoryTokens { get; } = new();
    public ObservableRangeCollection<ScewinToken> FilteredGeneralMemoryTokens { get; } = new();
    public ObservableRangeCollection<ScewinToken> FilteredMemoryTokens { get; } = new();

    public bool HasFilteredPrimaryCbsTokens => FilteredPrimaryCbsMemoryTokens.Count > 0;
    public bool HasFilteredMsiOverlayTokens => FilteredMsiOverlayMemoryTokens.Count > 0;
    public bool HasFilteredPbsDuplicateTokens => FilteredPbsDuplicateMemoryTokens.Count > 0;
    public bool HasFilteredGeneralMemoryTokens => FilteredGeneralMemoryTokens.Count > 0;
    public bool HasFilteredMemoryTokens => FilteredMemoryTokens.Count > 0;

    [RelayCommand]
    public void SetOverclockingSubCategory(string subCategory) => OverclockingSubCategory = subCategory;

    [RelayCommand]
    public void SetCpuPowerSubCategory(string subCategory) => CpuPowerSubCategory = subCategory;

    [RelayCommand]
    public void SetPcieSubCategory(string subCategory) => PcieSubCategory = subCategory;

    [RelayCommand]
    public void SetMemorySubCategory(string subCategory) => MemorySubCategory = subCategory;

    partial void OnOverclockingSubCategoryChanged(string value) => UpdateOverclockingFilter();
    partial void OnCpuPowerSubCategoryChanged(string value) => UpdateCpuPowerFilter();
    partial void OnPcieSubCategoryChanged(string value) => UpdatePcieFilter();
    partial void OnMemorySubCategoryChanged(string value) => UpdateMemoryFilter();

    // Raw tab filtering
    [ObservableProperty]
    private string _rawSearchQuery = string.Empty;

    [ObservableProperty]
    private string _rawCategoryFilter = "All";

    [ObservableProperty]
    private bool _rawOnlyModified;

    private CancellationTokenSource? _searchCts;

    public int SearchDebounceDelayMs { get; set; } = 160;

    public Task? CurrentSearchTask { get; private set; }

    partial void OnRawSearchQueryChanged(string value) => TriggerRawFilterDebounced();
    partial void OnRawCategoryFilterChanged(string value) => TriggerRawFilterDebounced();
    partial void OnRawOnlyModifiedChanged(bool value) => TriggerRawFilterDebounced();

    public ObservableRangeCollection<ScewinToken> FilteredRawTokens { get; } = new();

    public MainViewModel() : this(
        new ScewinParser(),
        new ScewinDetector(),
        new ScewinRunner(),
        new SettingsService(),
        new ScewinDownloader(),
        LocalizationService.Instance,
        new DumpComparer())
    {
    }

    public MainViewModel(
        IScewinParser parser,
        IScewinDetector detector,
        IScewinRunner runner,
        ISettingsService settingsService,
        IScewinDownloader downloader,
        ILocalizationService l10n,
        IDumpComparer? dumpComparer = null)
    {
        _parser = parser;
        _detector = detector;
        _runner = runner;
        _settingsService = settingsService;
        _downloader = downloader;
        _dumpComparer = dumpComparer ?? new DumpComparer();
        L10n = l10n;

        L10n.LanguageChanged += () =>
        {
            OnPropertyChanged(nameof(L10n));
            OnPropertyChanged(nameof(StatusHeader));
        };

        PendingDiffs.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(ModifiedCount));
            OnPropertyChanged(nameof(HasModifiedItems));
        };

        InitializeAsync();
    }

    public string StatusHeader => CurrentDump != null
        ? L10n["Status_Connected"]
        : L10n["Status_NotConnected"];

    private async void InitializeAsync()
    {
        try
        {
            // Load saved settings
            if (!string.IsNullOrEmpty(_settingsService.Settings.Language))
            {
                L10n.CurrentLanguage = _settingsService.Settings.Language;
            }

            if (!string.IsNullOrEmpty(_settingsService.Settings.ScewinExePath))
            {
                var res = _detector.ValidatePath(_settingsService.Settings.ScewinExePath);
                if (res.Found)
                {
                    ApplyDetectionResult(res);
                }
            }

            if (!IsScewinReady && _settingsService.Settings.AutoScanOnStartup)
            {
                await RunAutoDetectAsync(silent: true);
            }

            UpdateMotherboardSummary();

            // Try auto-loading default or sample dump if available
            TryLoadDefaultDump();
        }
        catch (Exception ex)
        {
            App.LogCrash("MainViewModel.InitializeAsync", ex);
        }
    }

    private void TryLoadDefaultDump()
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var dumpFiles = Directory.GetFiles(baseDir, "nvram_dump_*.txt")
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .ToList();
            if (dumpFiles.Count > 0)
            {
                LoadDumpFromFile(dumpFiles[0]);
            }
        }
        catch { }
    }

    [RelayCommand]
    public async Task RunAutoDetectAsync(bool silent = false)
    {
        IsLoading = true;
        LoadingMessage = L10n["Btn_AutoDetect"] + "...";

        try
        {
            var result = await _detector.AutoDetectAsync();
            if (result.Found)
            {
                ApplyDetectionResult(result);
                _settingsService.Settings.ScewinExePath = result.ExePath;
                _settingsService.Save();

                if (!silent)
                {
                    MessageBox.Show(
                        $"{result.Details}\n{result.ExePath}",
                        L10n["Btn_AutoDetect"],
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            else if (!silent)
            {
                MessageBox.Show(
                    result.Details,
                    L10n["Btn_AutoDetect"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyDetectionResult(DetectionResult res)
    {
        ScewinPath = res.ExePath;
        IsScewinReady = res.Found;
        IsDriversReady = res.DriversReady;
        ScewinDetails = $"{res.Details} ({res.Version})";
    }

    [RelayCommand]
    public void BrowseScewinPath()
    {
        var ofd = new OpenFileDialog
        {
            Title = L10n["Btn_BrowseScewin"],
            Filter = "SCEWIN Executable (SCEWIN_64.exe)|SCEWIN*.exe|Executables (*.exe)|*.exe|All Files (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            var res = _detector.ValidatePath(ofd.FileName);
            ApplyDetectionResult(res);
            _settingsService.Settings.ScewinExePath = ofd.FileName;
            _settingsService.Save();
        }
    }

    [RelayCommand]
    public void OpenDumpFileDialog()
    {
        var ofd = new OpenFileDialog
        {
            Title = L10n["Btn_LoadDump"],
            Filter = "NVRAM Script Dump (*.txt)|*.txt|All Files (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            LoadDumpFromFile(ofd.FileName);
        }
    }

    public void LoadDumpFromFile(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            var dump = _parser.Parse(content, filePath);
            SetCurrentDump(dump);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading dump: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void SetCurrentDump(ScewinDump dump)
    {
        CurrentDump = dump;
        OnPropertyChanged(nameof(StatusHeader));

        AllTokens.Clear();
        AspmTokens.Clear();
        OverclockingTokens.Clear();
        MemoryTokens.Clear();
        PrimaryCbsMemoryTokens.Clear();
        MsiOverlayMemoryTokens.Clear();
        PbsDuplicateMemoryTokens.Clear();
        GeneralMemoryTokens.Clear();
        CpuTokens.Clear();
        PendingDiffs.Clear();

        if (ComparisonDumpA == null)
        {
            ComparisonDumpA = dump;
            ComparisonDumpAPath = !string.IsNullOrEmpty(dump.FilePath) ? dump.FilePath : "Текущий профиль BIOS";
            UpdateComparisonDumpAInfo();
        }

        foreach (var token in dump.Tokens)
        {
            token.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ScewinToken.IsModified))
                {
                    OnTokenModified(token);
                }
            };

            AllTokens.Add(token);

            switch (token.Category)
            {
                case "ASPM":
                    AspmTokens.Add(token);
                    break;
                case "Overclocking":
                    OverclockingTokens.Add(token);
                    break;
                case "Memory":
                    MemoryTokens.Add(token);
                    if (token.MemoryTier == MemoryTier.Tier1Cbs)
                    {
                        PrimaryCbsMemoryTokens.Add(token);
                    }
                    else if (token.MemoryTier == MemoryTier.Tier2Msi)
                    {
                        MsiOverlayMemoryTokens.Add(token);
                    }
                    else if (token.MemoryTier == MemoryTier.Tier3Pbs)
                    {
                        PbsDuplicateMemoryTokens.Add(token);
                    }
                    else
                    {
                        GeneralMemoryTokens.Add(token);
                    }
                    break;
                case "CpuPower":
                    CpuTokens.Add(token);
                    break;
            }
        }

        // Curate PrimaryCbsMemoryTokens so primary 5 timings appear at top:
        // Tcl (2C), Trcdrd (2D), Trcdwr (2E), Trp (2F), Tras (30)
        var priorityCbs = new List<ScewinToken>();
        var cbsTimingMap = new (string Id, string Name)[]
        {
            ("2C", "Tcl"),
            ("2D", "Trcdrd"),
            ("2E", "Trcdwr"),
            ("2F", "Trp"),
            ("30", "Tras")
        };
        foreach (var (id, name) in cbsTimingMap)
        {
            var t = PrimaryCbsMemoryTokens.FirstOrDefault(x =>
                string.Equals(x.TokenId, id, StringComparison.OrdinalIgnoreCase) &&
                (x.Question.Equals(name, StringComparison.OrdinalIgnoreCase) || x.Question.StartsWith(name, StringComparison.OrdinalIgnoreCase)));
            if (t == null)
            {
                t = PrimaryCbsMemoryTokens.FirstOrDefault(x => string.Equals(x.TokenId, id, StringComparison.OrdinalIgnoreCase));
            }
            if (t != null && !priorityCbs.Contains(t)) priorityCbs.Add(t);
        }
        if (priorityCbs.Count > 0)
        {
            var remainingCbs = PrimaryCbsMemoryTokens.Where(x => !priorityCbs.Contains(x)).ToList();
            PrimaryCbsMemoryTokens.Clear();
            foreach (var p in priorityCbs) PrimaryCbsMemoryTokens.Add(p);
            foreach (var r in remainingCbs) PrimaryCbsMemoryTokens.Add(r);
        }

        // Curate MsiOverlayMemoryTokens so primary 5 MSI timings appear at top:
        // tCL (2A28), tRCDRD (2A29), tRCDWR (2A2A), tRP (2A2B), tRAS (2A2C)
        var priorityMsi = new List<ScewinToken>();
        var msiTimingMap = new (string Id, string Name)[]
        {
            ("2A28", "tCL"),
            ("2A29", "tRCDRD"),
            ("2A2A", "tRCDWR"),
            ("2A2B", "tRP"),
            ("2A2C", "tRAS")
        };
        foreach (var (id, name) in msiTimingMap)
        {
            var t = MsiOverlayMemoryTokens.FirstOrDefault(x =>
                string.Equals(x.TokenId, id, StringComparison.OrdinalIgnoreCase) &&
                x.Question.TrimStart().StartsWith(name, StringComparison.OrdinalIgnoreCase));
            if (t == null)
            {
                t = MsiOverlayMemoryTokens.FirstOrDefault(x => string.Equals(x.TokenId, id, StringComparison.OrdinalIgnoreCase));
            }
            if (t != null && !priorityMsi.Contains(t)) priorityMsi.Add(t);
        }
        if (priorityMsi.Count > 0)
        {
            var remainingMsi = MsiOverlayMemoryTokens.Where(x => !priorityMsi.Contains(x)).ToList();
            MsiOverlayMemoryTokens.Clear();
            foreach (var p in priorityMsi) MsiOverlayMemoryTokens.Add(p);
            foreach (var r in remainingMsi) MsiOverlayMemoryTokens.Add(r);
        }

        // Curate PbsDuplicateMemoryTokens so primary 5 PBS timings appear at top:
        // Tcl (64), Trcdrd (65), Trcdwr (66), Trp (67), Tras (68)
        var priorityPbs = new List<ScewinToken>();
        var pbsTimingMap = new (string Id, string Name)[]
        {
            ("64", "Tcl"),
            ("65", "Trcdrd"),
            ("66", "Trcdwr"),
            ("67", "Trp"),
            ("68", "Tras")
        };
        foreach (var (id, name) in pbsTimingMap)
        {
            var t = PbsDuplicateMemoryTokens.FirstOrDefault(x =>
                string.Equals(x.TokenId, id, StringComparison.OrdinalIgnoreCase) &&
                (x.Question.Equals(name, StringComparison.OrdinalIgnoreCase) || x.Question.StartsWith(name, StringComparison.OrdinalIgnoreCase)));
            if (t == null)
            {
                t = PbsDuplicateMemoryTokens.FirstOrDefault(x => string.Equals(x.TokenId, id, StringComparison.OrdinalIgnoreCase));
            }
            if (t != null && !priorityPbs.Contains(t)) priorityPbs.Add(t);
        }
        if (priorityPbs.Count > 0)
        {
            var remainingPbs = PbsDuplicateMemoryTokens.Where(x => !priorityPbs.Contains(x)).ToList();
            PbsDuplicateMemoryTokens.Clear();
            foreach (var p in priorityPbs) PbsDuplicateMemoryTokens.Add(p);
            foreach (var r in remainingPbs) PbsDuplicateMemoryTokens.Add(r);
        }

        OnPropertyChanged(nameof(HasPrimaryCbsTokens));
        OnPropertyChanged(nameof(HasMsiOverlayTokens));
        OnPropertyChanged(nameof(HasPbsDuplicateTokens));
        OnPropertyChanged(nameof(HasGeneralMemoryTokens));

        // Curate AspmTokens so the primary PCIe latency optimization settings appear at the top:
        // 1. ASPM / Active State Power Management
        // 2. PCIe Slot Bifurcation / Lanes Configuration / Link Speed
        // 3. Re-Size BAR Support / Above 4G Decoding
        var priorityAspm = new List<ScewinToken>();

        var aspm = AspmTokens.FirstOrDefault(t =>
            t.Question.Equals("Active State Power Management (ASPM)", StringComparison.OrdinalIgnoreCase) ||
            t.Question.Equals("ASPM Mode Control", StringComparison.OrdinalIgnoreCase) ||
            t.Question.Equals("ASPM Support", StringComparison.OrdinalIgnoreCase) ||
            t.Question.Equals("PM L1 SS", StringComparison.OrdinalIgnoreCase)) ??
            dump.Tokens.FirstOrDefault(t => t.Category == "ASPM" && (
                t.Question.Equals("Active State Power Management (ASPM)", StringComparison.OrdinalIgnoreCase) ||
                t.Question.Equals("ASPM Mode Control", StringComparison.OrdinalIgnoreCase) ||
                t.Question.Equals("ASPM Support", StringComparison.OrdinalIgnoreCase) ||
                t.Question.Equals("PM L1 SS", StringComparison.OrdinalIgnoreCase)));
        if (aspm != null) priorityAspm.Add(aspm);

        var bifurc = AspmTokens.FirstOrDefault(t =>
            t.Question.Contains("Bifurcation", StringComparison.OrdinalIgnoreCase) ||
            t.Question.Equals("PCIe/GFX Lanes Configuration", StringComparison.OrdinalIgnoreCase)) ??
            dump.Tokens.FirstOrDefault(t => t.Category == "ASPM" && (
                t.Question.Contains("Bifurcation", StringComparison.OrdinalIgnoreCase) ||
                t.Question.Equals("PCIe/GFX Lanes Configuration", StringComparison.OrdinalIgnoreCase)));
        if (bifurc != null && !priorityAspm.Contains(bifurc)) priorityAspm.Add(bifurc);

        var resizeBar = AspmTokens.FirstOrDefault(t =>
            t.Question.Equals("Re-Size BAR Support", StringComparison.OrdinalIgnoreCase) ||
            t.Question.Equals("Above 4G Decoding", StringComparison.OrdinalIgnoreCase)) ??
            dump.Tokens.FirstOrDefault(t => t.Category == "ASPM" && (
                t.Question.Equals("Re-Size BAR Support", StringComparison.OrdinalIgnoreCase) ||
                t.Question.Equals("Above 4G Decoding", StringComparison.OrdinalIgnoreCase)));
        if (resizeBar != null && !priorityAspm.Contains(resizeBar)) priorityAspm.Add(resizeBar);

        if (priorityAspm.Count > 0)
        {
            var remaining = AspmTokens.Where(t => !priorityAspm.Contains(t)).ToList();
            AspmTokens.Clear();
            foreach (var p in priorityAspm) AspmTokens.Add(p);
            foreach (var r in remaining) AspmTokens.Add(r);
        }

        UpdateOverclockingFilter();
        UpdateCpuPowerFilter();
        UpdatePcieFilter();
        UpdateMemoryFilter();
        UpdateRawFilter();
        OnPropertyChanged(nameof(ModifiedCount));
        OnPropertyChanged(nameof(HasModifiedItems));
    }

    private void OnTokenModified(ScewinToken token)
    {
        var existing = PendingDiffs.FirstOrDefault(d => d.TokenId == token.TokenId);
        if (token.IsModified)
        {
            if (existing != null)
            {
                existing.NewValue = token.CurrentDisplayValue;
            }
            else
            {
                PendingDiffs.Add(new ScewinDiffItem
                {
                    TokenId = token.TokenId,
                    Question = token.DisplayTitle,
                    OldValue = token.OriginalDisplayValue,
                    NewValue = token.CurrentDisplayValue,
                    Offset = token.Offset,
                    TokenRef = token
                });
            }
        }
        else
        {
            if (existing != null)
            {
                PendingDiffs.Remove(existing);
            }
        }

        OnPropertyChanged(nameof(ModifiedCount));
        OnPropertyChanged(nameof(HasModifiedItems));
    }

    [RelayCommand]
    public void ResetAllChanges()
    {
        if (CurrentDump == null) return;
        foreach (var t in CurrentDump.Tokens)
        {
            t.Reset();
        }
        PendingDiffs.Clear();
        OnPropertyChanged(nameof(ModifiedCount));
        OnPropertyChanged(nameof(HasModifiedItems));
    }

    [RelayCommand]
    public async Task ExportFromBiosAsync()
    {
        if (!IsAdmin)
        {
            var answer = MessageBox.Show(
                "Для прямого считывания NVRAM из BIOS через низкоуровневые драйверы AMI требуются права Администратора.\n\nПерезапустить SCEWIN Studio от имени Администратора?",
                "Требуются права Администратора",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (answer == MessageBoxResult.Yes)
            {
                RestartAsAdmin();
            }
            return;
        }

        if (!IsScewinReady || string.IsNullOrEmpty(ScewinPath))
        {
            MessageBox.Show(L10n["Settings_ScewinPathDesc"], "SCEWIN Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var saveDialog = new SaveFileDialog
        {
            Title = L10n["Btn_ExportNvram"],
            Filter = "NVRAM Script Dump (*.txt)|*.txt",
            FileName = $"nvram_dump_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (saveDialog.ShowDialog() == true)
        {
            IsLoading = true;
            LoadingMessage = "Считывание NVRAM из BIOS через SCEWIN /o...";

            try
            {
                var result = await _runner.ExportNvramAsync(ScewinPath, saveDialog.FileName);
                if (result.success && File.Exists(saveDialog.FileName))
                {
                    LoadDumpFromFile(saveDialog.FileName);
                    MessageBox.Show(L10n["Dialog_ExportSuccess"], "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(L10n.Get("Dialog_ExportError", result.error + "\n" + result.output), "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    private async Task<(bool success, string backupPath)> ExecuteApplyCoreAsync()
    {
        if (CurrentDump == null || PendingDiffs.Count == 0)
        {
            MessageBox.Show(L10n["Diff_Empty"], "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            return (false, string.Empty);
        }

        if (!IsAdmin)
        {
            var answer = MessageBox.Show(
                "Для записи изменений в NVRAM BIOS через низкоуровневые драйверы AMI требуются права Администратора.\n\nПерезапустить SCEWIN Studio от имени Администратора?",
                "Требуются права Администратора",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (answer == MessageBoxResult.Yes)
            {
                RestartAsAdmin();
            }
            return (false, string.Empty);
        }

        if (!IsScewinReady || string.IsNullOrEmpty(ScewinPath))
        {
            MessageBox.Show(L10n["Settings_ScewinPathDesc"], "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return (false, string.Empty);
        }

        var diffSummary = string.Join("\n", PendingDiffs.Select(d => $"• {d.Question}: {d.OldValue} → {d.NewValue}"));
        var confirmMsg = L10n.Get("Dialog_ApplyConfirmText", diffSummary);

        var confirm = MessageBox.Show(confirmMsg, L10n["Dialog_ApplyConfirmTitle"], MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return (false, string.Empty);

        IsLoading = true;
        LoadingMessage = "Применение изменений в NVRAM...";

        try
        {
            // 1. Generate timestamped backup
            var dumpDir = Path.GetDirectoryName(CurrentDump.FilePath);
            if (string.IsNullOrEmpty(dumpDir) || !Directory.Exists(dumpDir))
            {
                dumpDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
                Directory.CreateDirectory(dumpDir);
            }

            var backupPath = Path.Combine(dumpDir, $"nvram_backup_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            var backupScript = _parser.GenerateFullScript(CurrentDump);
            File.WriteAllText(backupPath, backupScript, new UTF8Encoding(false));

            // 2. Generate diff script
            var modifiedTokens = PendingDiffs.Select(d => d.TokenRef);
            var diffScript = _parser.GenerateDiffScript(CurrentDump, modifiedTokens);
            var diffPath = Path.Combine(Path.GetTempPath(), $"nvram_diff_{Guid.NewGuid():N}.txt");
            File.WriteAllText(diffPath, diffScript, new UTF8Encoding(false));

            // 3. Execute SCEWIN /i
            var runRes = await _runner.ImportNvramAsync(ScewinPath, diffPath);
            if (runRes.success)
            {
                // Commit original values to current
                foreach (var diff in PendingDiffs.ToList())
                {
                    if (diff.TokenRef.HasOptions)
                    {
                        diff.TokenRef.OriginalOption = diff.TokenRef.CurrentOption;
                    }
                    else
                    {
                        diff.TokenRef.OriginalNumericValue = diff.TokenRef.CustomNumericValue;
                    }
                }
                PendingDiffs.Clear();
                return (true, backupPath);
            }
            else
            {
                MessageBox.Show(
                    L10n.Get("Dialog_ApplyError", runRes.error + "\n" + runRes.output),
                    "SCEWIN Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return (false, string.Empty);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ApplyChangesAsync()
    {
        var (success, backupPath) = await ExecuteApplyCoreAsync();
        if (success)
        {
            MessageBox.Show(
                $"{L10n["Dialog_ApplySuccess"]}\n\n{L10n.Get("Dialog_BackupSuccess", backupPath)}",
                "SCEWIN Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    public async Task ApplyAndRebootAsync()
    {
        var (success, backupPath) = await ExecuteApplyCoreAsync();
        if (success)
        {
            var prompt = $"{L10n["Dialog_ApplySuccess"]}\n\n{L10n.Get("Dialog_BackupSuccess", backupPath)}\n\nХотите перезагрузить компьютер прямо сейчас, чтобы применить изменения в BIOS?";
            var res = MessageBox.Show(prompt, "Перезагрузка ПК", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "shutdown",
                        Arguments = "/r /t 5 /c \"SCEWIN Studio: Перезагрузка для применения настроек UEFI NVRAM\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process.Start(psi);
                    Application.Current?.Shutdown();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось выполнить команду перезагрузки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }

    [RelayCommand]
    public void ExportDiffScript()
    {
        if (CurrentDump == null || PendingDiffs.Count == 0) return;

        var sfd = new SaveFileDialog
        {
            Title = L10n["Btn_ExportDiffScript"],
            Filter = "NVRAM Script (*.txt)|*.txt",
            FileName = "nvram_diff.txt"
        };

        if (sfd.ShowDialog() == true)
        {
            var script = _parser.GenerateDiffScript(CurrentDump, PendingDiffs.Select(d => d.TokenRef));
            File.WriteAllText(sfd.FileName, script);
            MessageBox.Show($"Diff script exported to:\n{sfd.FileName}", "Exported", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    public void ImportDiff()
    {
        OpenDumpFileDialog();
    }

    [RelayCommand]
    public void CreateBackup()
    {
        if (CurrentDump == null)
        {
            MessageBox.Show(L10n["Status_NotConnected"], "Backup", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var dumpDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
            Directory.CreateDirectory(dumpDir);
            var backupPath = Path.Combine(dumpDir, $"nvram_backup_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            var backupScript = _parser.GenerateFullScript(CurrentDump);
            File.WriteAllText(backupPath, backupScript);
            MessageBox.Show(L10n.Get("Dialog_BackupSuccess", backupPath), "Backup Created", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error creating backup: {ex.Message}", "Backup Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void UpdateMotherboardSummary()
    {
        string boardName = string.Empty;
        string biosText = string.Empty;

        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
            if (key != null)
            {
                var mfr = key.GetValue("BaseBoardManufacturer")?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(mfr) || mfr.Contains("To be filled", StringComparison.OrdinalIgnoreCase))
                {
                    mfr = key.GetValue("SystemManufacturer")?.ToString()?.Trim() ?? "";
                }

                var prod = key.GetValue("BaseBoardProduct")?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(prod) || prod.Contains("To be filled", StringComparison.OrdinalIgnoreCase))
                {
                    prod = key.GetValue("SystemProductName")?.ToString()?.Trim() ?? "";
                }

                var ver = key.GetValue("BIOSVersion")?.ToString()?.Trim() ?? "";

                // Normalize vendor names
                string cleanMfr = mfr switch
                {
                    var s when s.Contains("ASUSTeK", StringComparison.OrdinalIgnoreCase) || s.Contains("ASUS", StringComparison.OrdinalIgnoreCase) => "ASUS",
                    var s when s.Contains("Micro-Star", StringComparison.OrdinalIgnoreCase) || s.Contains("MSI", StringComparison.OrdinalIgnoreCase) => "MSI",
                    var s when s.Contains("Gigabyte", StringComparison.OrdinalIgnoreCase) => "GIGABYTE",
                    var s when s.Contains("ASRock", StringComparison.OrdinalIgnoreCase) => "ASRock",
                    var s when s.Contains("EVGA", StringComparison.OrdinalIgnoreCase) => "EVGA",
                    var s when s.Contains("NZXT", StringComparison.OrdinalIgnoreCase) => "NZXT",
                    var s when s.Contains("Colorful", StringComparison.OrdinalIgnoreCase) => "COLORFUL",
                    var s when s.Contains("Biostar", StringComparison.OrdinalIgnoreCase) => "BIOSTAR",
                    _ => mfr
                };

                // Clean product name (e.g. "MPG B550 GAMING PLUS (MS-7C56)" -> "MPG B550 GAMING PLUS")
                string cleanProd = prod;
                if (cleanProd.Contains("MS-") && cleanProd.Contains("("))
                {
                    cleanProd = cleanProd.Split('(')[0].Trim();
                }

                if (!string.IsNullOrEmpty(cleanProd))
                {
                    if (!string.IsNullOrEmpty(cleanMfr) &&
                        !cleanProd.StartsWith(cleanMfr, StringComparison.OrdinalIgnoreCase) &&
                        !cleanMfr.Contains("To be filled", StringComparison.OrdinalIgnoreCase))
                    {
                        boardName = $"{cleanMfr} {cleanProd}";
                    }
                    else
                    {
                        boardName = cleanProd;
                    }
                }
                else if (!string.IsNullOrEmpty(cleanMfr) && !cleanMfr.Contains("To be filled", StringComparison.OrdinalIgnoreCase))
                {
                    boardName = cleanMfr;
                }

                if (!string.IsNullOrEmpty(ver) && !ver.Contains("To be filled", StringComparison.OrdinalIgnoreCase))
                {
                    biosText = $"BIOS {ver}";
                }
            }
        }
        catch { }

        if (string.IsNullOrEmpty(boardName) || boardName.Contains("To be filled", StringComparison.OrdinalIgnoreCase))
        {
            boardName = "Системная плата";
        }

        if (string.IsNullOrEmpty(biosText))
        {
            biosText = "UEFI BIOS";
        }

        var crcInfo = CurrentDump != null
            ? $"CRC32: {CurrentDump.HiiCrc32}"
            : (L10n.IsRussian ? "Дамп не загружен" : "No dump loaded");

        MotherboardSummary = $"{boardName} ({biosText} | {crcInfo})";
    }

    [RelayCommand]
    public void ToggleLanguage()
    {
        L10n.CurrentLanguage = L10n.IsRussian ? "en-US" : "ru-RU";
        _settingsService.Settings.Language = L10n.CurrentLanguage;
        _settingsService.Save();
    }

    public void SetLanguage(string lang)
    {
        L10n.CurrentLanguage = lang;
        _settingsService.Settings.Language = lang;
        _settingsService.Save();
    }

    [RelayCommand]
    public void RestartAsAdmin()
    {
        try
        {
            var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);
                Application.Current?.Shutdown();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось перезапустить от имени Администратора: {ex.Message}", "Ошибка UAC", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    public async Task DownloadLatestScewinAsync()
    {
        var targetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "SCEWIN");
        var progress = new Progress<(int percentage, string status)>(update =>
        {
            LoadingMessage = $"{update.status} ({update.percentage}%)";
        });

        IsLoading = true;
        try
        {
            var res = await _downloader.DownloadOrProvisionScewinAsync(targetDir, progress);
            if (res.success)
            {
                var detectRes = _detector.ValidatePath(res.exePath);
                ApplyDetectionResult(detectRes);
                _settingsService.Settings.ScewinExePath = res.exePath;
                _settingsService.Save();

                MessageBox.Show(
                    $"{L10n["Dialog_DownloadSuccess"]}\n{res.message}",
                    "Download Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    L10n.Get("Dialog_DownloadError", res.message),
                    "Download Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateOverclockingFilter()
    {
        if (CurrentDump == null)
        {
            FilteredOverclockingTokens.ReplaceRange(Array.Empty<ScewinToken>());
            return;
        }

        var filter = OverclockingSubCategory ?? "All";
        if (filter == "All")
        {
            FilteredOverclockingTokens.ReplaceRange(OverclockingTokens);
        }
        else
        {
            var matches = new List<ScewinToken>();
            var seen = new HashSet<ScewinToken>();
            foreach (var t in OverclockingTokens)
            {
                if (string.Equals(t.SubCategory, filter, StringComparison.OrdinalIgnoreCase) && seen.Add(t))
                {
                    matches.Add(t);
                }
            }
            foreach (var t in CpuTokens)
            {
                if (string.Equals(t.SubCategory, filter, StringComparison.OrdinalIgnoreCase) && seen.Add(t))
                {
                    matches.Add(t);
                }
            }
            FilteredOverclockingTokens.ReplaceRange(matches);
        }
    }

    private void UpdateCpuPowerFilter()
    {
        if (CurrentDump == null)
        {
            FilteredCpuTokens.ReplaceRange(Array.Empty<ScewinToken>());
            return;
        }

        var filter = CpuPowerSubCategory ?? "All";
        if (filter == "All")
        {
            FilteredCpuTokens.ReplaceRange(CpuTokens);
        }
        else
        {
            var matches = new List<ScewinToken>();
            var seen = new HashSet<ScewinToken>();
            foreach (var t in CpuTokens)
            {
                if (string.Equals(t.SubCategory, filter, StringComparison.OrdinalIgnoreCase) && seen.Add(t))
                {
                    matches.Add(t);
                }
            }
            foreach (var t in OverclockingTokens)
            {
                if (string.Equals(t.SubCategory, filter, StringComparison.OrdinalIgnoreCase) && seen.Add(t))
                {
                    matches.Add(t);
                }
            }
            FilteredCpuTokens.ReplaceRange(matches);
        }
    }

    private void UpdatePcieFilter()
    {
        if (CurrentDump == null)
        {
            FilteredAspmTokens.ReplaceRange(Array.Empty<ScewinToken>());
            return;
        }

        var filter = PcieSubCategory ?? "All";
        if (filter == "All")
        {
            FilteredAspmTokens.ReplaceRange(AspmTokens);
        }
        else
        {
            var matches = new List<ScewinToken>();
            foreach (var t in AspmTokens)
            {
                if (string.Equals(t.SubCategory, filter, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(t);
                }
            }
            FilteredAspmTokens.ReplaceRange(matches);
        }
    }

    private void UpdateMemoryFilter()
    {
        if (CurrentDump == null)
        {
            FilteredPrimaryCbsMemoryTokens.ReplaceRange(Array.Empty<ScewinToken>());
            FilteredMsiOverlayMemoryTokens.ReplaceRange(Array.Empty<ScewinToken>());
            FilteredPbsDuplicateMemoryTokens.ReplaceRange(Array.Empty<ScewinToken>());
            FilteredGeneralMemoryTokens.ReplaceRange(Array.Empty<ScewinToken>());
            FilteredMemoryTokens.ReplaceRange(Array.Empty<ScewinToken>());
            OnPropertyChanged(nameof(HasFilteredPrimaryCbsTokens));
            OnPropertyChanged(nameof(HasFilteredMsiOverlayTokens));
            OnPropertyChanged(nameof(HasFilteredPbsDuplicateTokens));
            OnPropertyChanged(nameof(HasFilteredGeneralMemoryTokens));
            OnPropertyChanged(nameof(HasFilteredMemoryTokens));
            return;
        }

        var filter = MemorySubCategory ?? "All";

        if (filter == "All")
        {
            FilteredPrimaryCbsMemoryTokens.ReplaceRange(PrimaryCbsMemoryTokens);
            FilteredMsiOverlayMemoryTokens.ReplaceRange(MsiOverlayMemoryTokens);
            FilteredPbsDuplicateMemoryTokens.ReplaceRange(PbsDuplicateMemoryTokens);
            FilteredGeneralMemoryTokens.ReplaceRange(GeneralMemoryTokens);
            FilteredMemoryTokens.ReplaceRange(MemoryTokens);
        }
        else
        {
            var cbs = new List<ScewinToken>();
            foreach (var t in PrimaryCbsMemoryTokens)
            {
                if (string.Equals(t.SubCategory, filter, StringComparison.OrdinalIgnoreCase))
                    cbs.Add(t);
            }
            FilteredPrimaryCbsMemoryTokens.ReplaceRange(cbs);

            var msi = new List<ScewinToken>();
            foreach (var t in MsiOverlayMemoryTokens)
            {
                if (string.Equals(t.SubCategory, filter, StringComparison.OrdinalIgnoreCase))
                    msi.Add(t);
            }
            FilteredMsiOverlayMemoryTokens.ReplaceRange(msi);

            var pbs = new List<ScewinToken>();
            foreach (var t in PbsDuplicateMemoryTokens)
            {
                if (string.Equals(t.SubCategory, filter, StringComparison.OrdinalIgnoreCase))
                    pbs.Add(t);
            }
            FilteredPbsDuplicateMemoryTokens.ReplaceRange(pbs);

            var gen = new List<ScewinToken>();
            foreach (var t in GeneralMemoryTokens)
            {
                if (string.Equals(t.SubCategory, filter, StringComparison.OrdinalIgnoreCase))
                    gen.Add(t);
            }
            FilteredGeneralMemoryTokens.ReplaceRange(gen);

            var mem = new List<ScewinToken>();
            foreach (var t in MemoryTokens)
            {
                if (string.Equals(t.SubCategory, filter, StringComparison.OrdinalIgnoreCase))
                    mem.Add(t);
            }
            FilteredMemoryTokens.ReplaceRange(mem);
        }

        OnPropertyChanged(nameof(HasFilteredPrimaryCbsTokens));
        OnPropertyChanged(nameof(HasFilteredMsiOverlayTokens));
        OnPropertyChanged(nameof(HasFilteredPbsDuplicateTokens));
        OnPropertyChanged(nameof(HasFilteredGeneralMemoryTokens));
        OnPropertyChanged(nameof(HasFilteredMemoryTokens));
    }

    public void TriggerRawFilterDebounced(int? overrideDelayMs = null)
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();

        var cts = new CancellationTokenSource();
        _searchCts = cts;
        var token = cts.Token;
        int delay = overrideDelayMs ?? SearchDebounceDelayMs;

        CurrentSearchTask = ExecuteSearchFilterAsync(delay, token);
    }

    private async Task ExecuteSearchFilterAsync(int delayMs, CancellationToken cancellationToken)
    {
        try
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested) return;

            var tokens = CurrentDump?.Tokens;
            if (tokens == null || tokens.Count == 0)
            {
                FilteredRawTokens.ReplaceRange(Array.Empty<ScewinToken>());
                return;
            }

            var query = (RawSearchQuery ?? string.Empty).Trim();
            var category = RawCategoryFilter ?? "All";
            var onlyModified = RawOnlyModified;

            // Perform 4000-token search matching on background thread Task.Run
            var matches = await Task.Run(() =>
            {
                var result = new List<ScewinToken>();
                foreach (var t in tokens)
                {
                    if (cancellationToken.IsCancellationRequested) return result;

                    if (onlyModified && !t.IsModified) continue;

                    if (category != "All" &&
                        !string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(t.SubCategory, category, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(query))
                    {
                        bool match = t.Question.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                     t.TokenId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                     t.Offset.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                     t.HelpString.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                     t.CurrentDisplayValue.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                     t.SubCategory.Contains(query, StringComparison.OrdinalIgnoreCase);

                        if (!match) continue;
                    }

                    result.Add(t);
                }
                return result;
            }, cancellationToken);

            if (cancellationToken.IsCancellationRequested) return;

            // Call FilteredRawTokens.ReplaceRange(matches) on the UI Dispatcher
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                await dispatcher.InvokeAsync(() =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        FilteredRawTokens.ReplaceRange(matches);
                    }
                });
            }
            else
            {
                FilteredRawTokens.ReplaceRange(matches);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected cancellation on debouncing
        }
    }

    public void UpdateRawFilter(bool immediate = true)
    {
        if (immediate)
        {
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = null;

            var tokens = CurrentDump?.Tokens;
            if (tokens == null || tokens.Count == 0)
            {
                FilteredRawTokens.ReplaceRange(Array.Empty<ScewinToken>());
                return;
            }

            var query = (RawSearchQuery ?? string.Empty).Trim();
            var category = RawCategoryFilter ?? "All";
            var onlyModified = RawOnlyModified;

            var matches = new List<ScewinToken>();
            foreach (var t in tokens)
            {
                if (onlyModified && !t.IsModified) continue;

                if (category != "All" &&
                    !string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(t.SubCategory, category, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(query))
                {
                    bool match = t.Question.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                 t.TokenId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                 t.Offset.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                 t.HelpString.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                 t.CurrentDisplayValue.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                 t.SubCategory.Contains(query, StringComparison.OrdinalIgnoreCase);

                    if (!match) continue;
                }

                matches.Add(t);
            }

            FilteredRawTokens.ReplaceRange(matches);
        }
        else
        {
            TriggerRawFilterDebounced();
        }
    }

    // ==========================================
    // Dual Dump Comparison Commands & Logic
    // ==========================================

    private CancellationTokenSource? _comparisonSearchCts;

    [RelayCommand]
    public void LoadComparisonDumpA()
    {
        var ofd = new OpenFileDialog
        {
            Title = "Выберите дамп BIOS для Профиля А",
            Filter = "BIOS NVRAM Dumps (*.txt)|*.txt|All Files (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                var content = File.ReadAllText(ofd.FileName);
                var dump = _parser.Parse(content, ofd.FileName);
                ComparisonDumpA = dump;
                ComparisonDumpAPath = ofd.FileName;
                UpdateComparisonDumpAInfo();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке дампа: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    public void UseCurrentDumpAsA()
    {
        if (CurrentDump == null)
        {
            MessageBox.Show("Текущий дамп BIOS не загружен. Загрузите дамп или считайте его из BIOS.", "Сравнение дампов", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ComparisonDumpA = CurrentDump;
        ComparisonDumpAPath = !string.IsNullOrEmpty(CurrentDump.FilePath) ? CurrentDump.FilePath : "Текущий профиль BIOS";
        UpdateComparisonDumpAInfo();
    }

    [RelayCommand]
    public void LoadComparisonDumpB()
    {
        var ofd = new OpenFileDialog
        {
            Title = "Выберите дамп BIOS для Профиля Б (например, профиль друга)",
            Filter = "BIOS NVRAM Dumps (*.txt)|*.txt|All Files (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                var content = File.ReadAllText(ofd.FileName);
                var dump = _parser.Parse(content, ofd.FileName);
                ComparisonDumpB = dump;
                ComparisonDumpBPath = ofd.FileName;
                UpdateComparisonDumpBInfo();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке дампа: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    public void RunComparison()
    {
        if (ComparisonDumpA == null || ComparisonDumpB == null)
        {
            MessageBox.Show("Пожалуйста, загрузите оба дампа (Профиль А и Профиль Б) перед запуском сравнения.", "Сравнение дампов", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var items = _dumpComparer.Compare(ComparisonDumpA, ComparisonDumpB);
        TotalComparedCount = items.Count;
        DifferentCount = items.Count(i => i.IsDifferent);
        EqualCount = items.Count(i => i.IsEqual);
        OnlyInACount = items.Count(i => i.IsOnlyInA);
        OnlyInBCount = items.Count(i => i.IsOnlyInB);

        OnPropertyChanged(nameof(HasComparisonResults));

        AllComparisonItems.ReplaceRange(items);
        UpdateComparisonFilter(immediate: true);
    }

    [RelayCommand]
    public void SetComparisonFilter(string filter)
    {
        ComparisonFilter = filter;
    }

    public void TriggerComparisonFilterDebounced(int delayMs = 120)
    {
        _comparisonSearchCts?.Cancel();
        _comparisonSearchCts?.Dispose();

        var cts = new CancellationTokenSource();
        _comparisonSearchCts = cts;
        var token = cts.Token;

        _ = ExecuteComparisonFilterAsync(delayMs, token);
    }

    private async Task ExecuteComparisonFilterAsync(int delayMs, CancellationToken cancellationToken)
    {
        try
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested) return;

            var all = AllComparisonItems.ToList();
            var filter = ComparisonFilter ?? "All";
            var query = (ComparisonSearchQuery ?? string.Empty).Trim();

            var matches = await Task.Run(() =>
            {
                var list = new List<DumpComparisonItem>();
                foreach (var item in all)
                {
                    if (cancellationToken.IsCancellationRequested) return list;

                    // Status filter
                    if (filter == "Different" && !item.IsDifferent) continue;
                    if (filter == "Equal" && !item.IsEqual) continue;
                    if (filter == "OnlyA" && !item.IsOnlyInA) continue;
                    if (filter == "OnlyB" && !item.IsOnlyInB) continue;

                    // Search query filter
                    if (!string.IsNullOrEmpty(query))
                    {
                        bool match = item.Question.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                     item.ValueA.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                     item.ValueB.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                     item.TokenA.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                     item.TokenB.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                     item.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                     item.SubCategory.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                     item.HelpString.Contains(query, StringComparison.OrdinalIgnoreCase);

                        if (!match) continue;
                    }

                    list.Add(item);
                }
                return list;
            }, cancellationToken);

            if (cancellationToken.IsCancellationRequested) return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                await dispatcher.InvokeAsync(() =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        FilteredComparisonItems.ReplaceRange(matches);
                    }
                });
            }
            else
            {
                FilteredComparisonItems.ReplaceRange(matches);
            }
        }
        catch (OperationCanceledException) { }
    }

    public void UpdateComparisonFilter(bool immediate = true)
    {
        if (immediate)
        {
            _comparisonSearchCts?.Cancel();
            _comparisonSearchCts?.Dispose();
            _comparisonSearchCts = null;

            var filter = ComparisonFilter ?? "All";
            var query = (ComparisonSearchQuery ?? string.Empty).Trim();

            var matches = new List<DumpComparisonItem>();
            foreach (var item in AllComparisonItems)
            {
                if (filter == "Different" && !item.IsDifferent) continue;
                if (filter == "Equal" && !item.IsEqual) continue;
                if (filter == "OnlyA" && !item.IsOnlyInA) continue;
                if (filter == "OnlyB" && !item.IsOnlyInB) continue;

                if (!string.IsNullOrEmpty(query))
                {
                    bool match = item.Question.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                 item.ValueA.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                 item.ValueB.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                 item.TokenA.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                 item.TokenB.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                 item.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                 item.SubCategory.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                 item.HelpString.Contains(query, StringComparison.OrdinalIgnoreCase);

                    if (!match) continue;
                }

                matches.Add(item);
            }

            FilteredComparisonItems.ReplaceRange(matches);
        }
        else
        {
            TriggerComparisonFilterDebounced();
        }
    }

    [RelayCommand]
    public void ExportComparisonReport()
    {
        if (AllComparisonItems.Count == 0)
        {
            MessageBox.Show("Нет данных для экспорта. Выполните сравнение двух дампов.", "Экспорт отчета", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var sfd = new SaveFileDialog
        {
            Title = "Сохранить отчет сравнения дампов BIOS",
            Filter = "Текстовый отчет (*.txt)|*.txt|Таблица CSV (*.csv)|*.csv|Все файлы (*.*)|*.*",
            FileName = $"BIOS_Comparison_Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                var isCsv = sfd.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
                var sb = new System.Text.StringBuilder();

                if (isCsv)
                {
                    sb.AppendLine("Статус,Категория,Параметр,Значение_в_Дамп_А,Токен_А,Смещение_А,Значение_в_Дамп_Б,Токен_Б,Смещение_Б,Описание");
                    foreach (var item in AllComparisonItems)
                    {
                        static string Escape(string s) => $"\"{s.Replace("\"", "\"\"")}\"";
                        sb.AppendLine($"{Escape(item.StatusBadgeText)},{Escape(item.Category)},{Escape(item.Question)},{Escape(item.ValueA)},{Escape(item.TokenA)},{Escape(item.OffsetA)},{Escape(item.ValueB)},{Escape(item.TokenB)},{Escape(item.OffsetB)},{Escape(item.HelpString)}");
                    }
                }
                else
                {
                    sb.AppendLine("================================================================================");
                    sb.AppendLine("SCEWIN Studio — Отчет сравнения двух дампов BIOS NVRAM");
                    sb.AppendLine("================================================================================");
                    sb.AppendLine($"Дата формирования : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine($"Профиль А         : {ComparisonDumpAPath}");
                    sb.AppendLine($"                    {ComparisonDumpAInfo}");
                    sb.AppendLine($"Профиль Б         : {ComparisonDumpBPath}");
                    sb.AppendLine($"                    {ComparisonDumpBInfo}");
                    sb.AppendLine("--------------------------------------------------------------------------------");
                    sb.AppendLine("СВОДНАЯ СТАТИСТИКА:");
                    sb.AppendLine($"• Всего параметров : {TotalComparedCount}");
                    sb.AppendLine($"• Различаются      : {DifferentCount}");
                    sb.AppendLine($"• Совпадают        : {EqualCount}");
                    sb.AppendLine($"• Только в А       : {OnlyInACount}");
                    sb.AppendLine($"• Только в Б       : {OnlyInBCount}");
                    sb.AppendLine("================================================================================");
                    sb.AppendLine();

                    var diffs = AllComparisonItems.Where(i => i.IsDifferent).ToList();
                    sb.AppendLine($"=== 1. РАЗЛИЧИЯ ({diffs.Count}) ===");
                    foreach (var d in diffs)
                    {
                        sb.AppendLine($"• [{d.Category}] {d.Question}");
                        sb.AppendLine($"    Дамп А : {d.ValueA}  (Token: 0x{d.TokenA}, Offset: 0x{d.OffsetA})");
                        sb.AppendLine($"    Дамп Б : {d.ValueB}  (Token: 0x{d.TokenB}, Offset: 0x{d.OffsetB})");
                    }
                    sb.AppendLine();

                    var onlyA = AllComparisonItems.Where(i => i.IsOnlyInA).ToList();
                    sb.AppendLine($"=== 2. ТОЛЬКО В ПРОФИЛЕ А ({onlyA.Count}) ===");
                    foreach (var a in onlyA)
                    {
                        sb.AppendLine($"• [{a.Category}] {a.Question} = {a.ValueA} (Token: 0x{a.TokenA}, Offset: 0x{a.OffsetA})");
                    }
                    sb.AppendLine();

                    var onlyB = AllComparisonItems.Where(i => i.IsOnlyInB).ToList();
                    sb.AppendLine($"=== 3. ТОЛЬКО В ПРОФИЛЕ Б ({onlyB.Count}) ===");
                    foreach (var b in onlyB)
                    {
                        sb.AppendLine($"• [{b.Category}] {b.Question} = {b.ValueB} (Token: 0x{b.TokenB}, Offset: 0x{b.OffsetB})");
                    }
                    sb.AppendLine();

                    var equals = AllComparisonItems.Where(i => i.IsEqual).ToList();
                    sb.AppendLine($"=== 4. СОВПАДАЮЩИЕ ПАРАМЕТРЫ ({equals.Count}) ===");
                    foreach (var eq in equals)
                    {
                        sb.AppendLine($"• [{eq.Category}] {eq.Question} = {eq.ValueA}");
                    }
                }

                File.WriteAllText(sfd.FileName, sb.ToString(), System.Text.Encoding.UTF8);
                MessageBox.Show($"Отчет успешно сохранен:\n{sfd.FileName}", "Отчет сохранен", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении отчета: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void UpdateComparisonDumpAInfo()
    {
        ComparisonDumpAInfo = FormatDumpSummary(ComparisonDumpA, ComparisonDumpAPath);
    }

    private void UpdateComparisonDumpBInfo()
    {
        ComparisonDumpBInfo = FormatDumpSummary(ComparisonDumpB, ComparisonDumpBPath);
    }

    private static string FormatDumpSummary(ScewinDump? dump, string path)
    {
        if (dump == null) return "Дамп не выбран";
        var fileName = !string.IsNullOrEmpty(path) ? Path.GetFileName(path) : "Дамп NVRAM";
        var tokenCount = $"{dump.Tokens.Count} параметров";
        var crc = !string.IsNullOrEmpty(dump.HiiCrc32) ? $"CRC32: {dump.HiiCrc32}" : string.Empty;
        var ver = !string.IsNullOrEmpty(dump.UtilityVersion) ? $"AMI v{dump.UtilityVersion}" : string.Empty;

        var parts = new List<string> { fileName, tokenCount };
        if (!string.IsNullOrEmpty(crc)) parts.Add(crc);
        if (!string.IsNullOrEmpty(ver)) parts.Add(ver);
        return string.Join(" • ", parts);
    }

    public void Dispose()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;

        _comparisonSearchCts?.Cancel();
        _comparisonSearchCts?.Dispose();
        _comparisonSearchCts = null;

        GC.SuppressFinalize(this);
    }
}
