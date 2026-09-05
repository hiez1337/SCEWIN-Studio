using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SCEWIN_Studio.Models;
using SCEWIN_Studio.Services;

namespace SCEWIN_Studio.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IScewinParser _parser;
    private readonly IScewinDetector _detector;
    private readonly IScewinRunner _runner;
    private readonly ISettingsService _settingsService;
    private readonly IScewinDownloader _downloader;

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
    private string _motherboardSummary = "MSI MPG B550 GAMING PLUS (BIOS 1.M2 | CRC32: F1F849CA)";

    [ObservableProperty]
    private string _navSearchQuery = string.Empty;

    public bool IsDumpLoaded => CurrentDump != null;
    public bool IsAdmin => _runner.IsAdministrator();

    public ObservableCollection<ScewinDiffItem> PendingDiffs { get; } = new();

    public int ModifiedCount => PendingDiffs.Count;
    public bool HasModifiedItems => PendingDiffs.Count > 0;

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

    public ObservableCollection<ScewinToken> FilteredOverclockingTokens { get; } = new();
    public ObservableCollection<ScewinToken> FilteredCpuTokens { get; } = new();
    public ObservableCollection<ScewinToken> FilteredAspmTokens { get; } = new();
    public ObservableCollection<ScewinToken> FilteredPrimaryCbsMemoryTokens { get; } = new();
    public ObservableCollection<ScewinToken> FilteredMsiOverlayMemoryTokens { get; } = new();
    public ObservableCollection<ScewinToken> FilteredPbsDuplicateMemoryTokens { get; } = new();
    public ObservableCollection<ScewinToken> FilteredGeneralMemoryTokens { get; } = new();
    public ObservableCollection<ScewinToken> FilteredMemoryTokens { get; } = new();

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

    public ObservableCollection<ScewinToken> FilteredRawTokens { get; } = new();

    public MainViewModel() : this(
        new ScewinParser(),
        new ScewinDetector(),
        new ScewinRunner(),
        new SettingsService(),
        new ScewinDownloader(),
        LocalizationService.Instance)
    {
    }

    public MainViewModel(
        IScewinParser parser,
        IScewinDetector detector,
        IScewinRunner runner,
        ISettingsService settingsService,
        IScewinDownloader downloader,
        ILocalizationService l10n)
    {
        _parser = parser;
        _detector = detector;
        _runner = runner;
        _settingsService = settingsService;
        _downloader = downloader;
        L10n = l10n;

        L10n.LanguageChanged += () =>
        {
            OnPropertyChanged(nameof(L10n));
            OnPropertyChanged(nameof(StatusHeader));
        };

        InitializeAsync();
    }

    public string StatusHeader => CurrentDump != null
        ? L10n["Status_Connected"]
        : L10n["Status_NotConnected"];

    private async void InitializeAsync()
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
                return;
            }
        }
        catch { }

        var possibleDumps = new List<string>
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nvramBEFORE.txt"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "nvramBEFORE.txt"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "src", "SCEWIN_Studio", "nvramBEFORE.txt"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "src", "SCEWIN_Studio", "nvramBEFORE.txt")
        };

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
        {
            possibleDumps.Add(Path.Combine(userProfile, "Downloads", "Скрипты и Конфиги", "SCEHUB-main", "SCEHUB-main", "SCEWIN", "5.05.01.0002", "nvramBEFORE.txt"));
        }

        foreach (var path in possibleDumps)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                LoadDumpFromFile(fullPath);
                break;
            }
        }
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

    [RelayCommand]
    public async Task ApplyChangesAsync()
    {
        if (CurrentDump == null || PendingDiffs.Count == 0)
        {
            MessageBox.Show(L10n["Diff_Empty"], "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
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
            return;
        }

        if (!IsScewinReady || string.IsNullOrEmpty(ScewinPath))
        {
            MessageBox.Show(L10n["Settings_ScewinPathDesc"], "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var diffSummary = string.Join("\n", PendingDiffs.Select(d => $"• {d.Question}: {d.OldValue} → {d.NewValue}"));
        var confirmMsg = L10n.Get("Dialog_ApplyConfirmText", diffSummary);

        var confirm = MessageBox.Show(confirmMsg, L10n["Dialog_ApplyConfirmTitle"], MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

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
            File.WriteAllText(backupPath, backupScript);

            // 2. Generate diff script
            var modifiedTokens = PendingDiffs.Select(d => d.TokenRef);
            var diffScript = _parser.GenerateDiffScript(CurrentDump, modifiedTokens);
            var diffPath = Path.Combine(Path.GetTempPath(), $"nvram_diff_{Guid.NewGuid():N}.txt");
            File.WriteAllText(diffPath, diffScript);

            // 3. Execute SCEWIN /i
            var runRes = await _runner.ImportNvramAsync(ScewinPath, diffPath);
            if (runRes.success)
            {
                MessageBox.Show(
                    $"{L10n["Dialog_ApplySuccess"]}\n\n{L10n.Get("Dialog_BackupSuccess", backupPath)}",
                    "SCEWIN Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Commit original values to current
                foreach (var diff in PendingDiffs.ToList())
                {
                    diff.TokenRef.OriginalOption = diff.TokenRef.CurrentOption;
                    diff.TokenRef.OriginalNumericValue = diff.TokenRef.CurrentDisplayValue;
                }
                PendingDiffs.Clear();
                OnPropertyChanged(nameof(ModifiedCount));
                OnPropertyChanged(nameof(HasModifiedItems));
            }
            else
            {
                MessageBox.Show(
                    L10n.Get("Dialog_ApplyError", runRes.error + "\n" + runRes.output),
                    "SCEWIN Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        finally
        {
            IsLoading = false;
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

    [RelayCommand]
    public async Task ApplyAndRebootAsync()
    {
        if (CurrentDump == null || PendingDiffs.Count == 0)
        {
            MessageBox.Show(L10n["Diff_Empty"], "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await ApplyChangesAsync();

        var res = MessageBox.Show(
            "Изменения NVRAM успешно применены в BIOS!\n\nПерезагрузить компьютер сейчас для активации новых параметров?",
            "Применение и Перезагрузка",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (res == MessageBoxResult.Yes)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "shutdown",
                    Arguments = "/r /t 5",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось инициировать перезагрузку: {ex.Message}", "Reboot", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    public void UpdateMotherboardSummary()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
            if (key != null)
            {
                var prod = key.GetValue("BaseBoardProduct")?.ToString()?.Trim();
                var ver = key.GetValue("BIOSVersion")?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(prod))
                {
                    var boardName = prod.Contains("MS-") ? prod.Split('(')[0].Trim() : prod;
                    if (!boardName.StartsWith("MSI", StringComparison.OrdinalIgnoreCase))
                        boardName = "MSI " + boardName;
                    var biosText = !string.IsNullOrEmpty(ver) ? $"BIOS {ver}" : "BIOS 1.M2";
                    var crc = CurrentDump?.HiiCrc32 ?? "F1F849CA";
                    MotherboardSummary = $"{boardName} ({biosText} | CRC32: {crc})";
                    return;
                }
            }
        }
        catch { }

        var defaultCrc = CurrentDump?.HiiCrc32 ?? "F1F849CA";
        MotherboardSummary = $"MSI MPG B550 GAMING PLUS (BIOS 1.M2 | CRC32: {defaultCrc})";
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
        FilteredOverclockingTokens.Clear();
        if (CurrentDump == null) return;

        var filter = OverclockingSubCategory ?? "All";
        if (filter == "All")
        {
            foreach (var t in OverclockingTokens)
            {
                FilteredOverclockingTokens.Add(t);
            }
        }
        else
        {
            var combined = OverclockingTokens.Concat(CpuTokens).Distinct();
            foreach (var t in combined)
            {
                if (string.Equals(t.SubCategory, filter, StringComparison.OrdinalIgnoreCase))
                {
                    FilteredOverclockingTokens.Add(t);
                }
            }
        }
    }

    private void UpdateCpuPowerFilter()
    {
        FilteredCpuTokens.Clear();
        if (CurrentDump == null) return;

        var filter = CpuPowerSubCategory ?? "All";
        if (filter == "All")
        {
            foreach (var t in CpuTokens)
            {
                FilteredCpuTokens.Add(t);
            }
        }
        else
        {
            var combined = CpuTokens.Concat(OverclockingTokens).Distinct();
            foreach (var t in combined)
            {
                if (string.Equals(t.SubCategory, filter, StringComparison.OrdinalIgnoreCase))
                {
                    FilteredCpuTokens.Add(t);
                }
            }
        }
    }

    private void UpdatePcieFilter()
    {
        FilteredAspmTokens.Clear();
        if (CurrentDump == null) return;

        var filter = PcieSubCategory ?? "All";
        foreach (var t in AspmTokens)
        {
            if (filter == "All" || string.Equals(t.SubCategory, filter, StringComparison.OrdinalIgnoreCase))
            {
                FilteredAspmTokens.Add(t);
            }
        }
    }

    private void UpdateMemoryFilter()
    {
        FilteredPrimaryCbsMemoryTokens.Clear();
        FilteredMsiOverlayMemoryTokens.Clear();
        FilteredPbsDuplicateMemoryTokens.Clear();
        FilteredGeneralMemoryTokens.Clear();
        FilteredMemoryTokens.Clear();

        if (CurrentDump == null) return;

        var filter = MemorySubCategory ?? "All";

        foreach (var t in PrimaryCbsMemoryTokens)
        {
            if (filter == "All" || string.Equals(t.SubCategory, filter, StringComparison.OrdinalIgnoreCase))
            {
                FilteredPrimaryCbsMemoryTokens.Add(t);
            }
        }

        foreach (var t in MsiOverlayMemoryTokens)
        {
            if (filter == "All" || string.Equals(t.SubCategory, filter, StringComparison.OrdinalIgnoreCase))
            {
                FilteredMsiOverlayMemoryTokens.Add(t);
            }
        }

        foreach (var t in PbsDuplicateMemoryTokens)
        {
            if (filter == "All" || string.Equals(t.SubCategory, filter, StringComparison.OrdinalIgnoreCase))
            {
                FilteredPbsDuplicateMemoryTokens.Add(t);
            }
        }

        foreach (var t in GeneralMemoryTokens)
        {
            if (filter == "All" || string.Equals(t.SubCategory, filter, StringComparison.OrdinalIgnoreCase))
            {
                FilteredGeneralMemoryTokens.Add(t);
            }
        }

        foreach (var t in MemoryTokens)
        {
            if (filter == "All" || string.Equals(t.SubCategory, filter, StringComparison.OrdinalIgnoreCase))
            {
                FilteredMemoryTokens.Add(t);
            }
        }

        OnPropertyChanged(nameof(HasFilteredPrimaryCbsTokens));
        OnPropertyChanged(nameof(HasFilteredMsiOverlayTokens));
        OnPropertyChanged(nameof(HasFilteredPbsDuplicateTokens));
        OnPropertyChanged(nameof(HasFilteredGeneralMemoryTokens));
        OnPropertyChanged(nameof(HasFilteredMemoryTokens));
    }

    partial void OnRawSearchQueryChanged(string value) => UpdateRawFilter();
    partial void OnRawCategoryFilterChanged(string value) => UpdateRawFilter();
    partial void OnRawOnlyModifiedChanged(bool value) => UpdateRawFilter();

    private void UpdateRawFilter()
    {
        FilteredRawTokens.Clear();
        if (CurrentDump == null) return;

        var q = (RawSearchQuery ?? "").Trim().ToLowerInvariant();
        var cat = RawCategoryFilter ?? "All";

        foreach (var t in CurrentDump.Tokens)
        {
            if (RawOnlyModified && !t.IsModified) continue;

            if (cat != "All" &&
                !string.Equals(t.Category, cat, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(t.SubCategory, cat, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(q))
            {
                bool match = t.Question.ToLowerInvariant().Contains(q) ||
                             t.TokenId.ToLowerInvariant().Contains(q) ||
                             t.Offset.ToLowerInvariant().Contains(q) ||
                             t.HelpString.ToLowerInvariant().Contains(q) ||
                             t.CurrentDisplayValue.ToLowerInvariant().Contains(q) ||
                             t.SubCategory.ToLowerInvariant().Contains(q);
                if (!match) continue;
            }

            FilteredRawTokens.Add(t);
        }
    }
}
