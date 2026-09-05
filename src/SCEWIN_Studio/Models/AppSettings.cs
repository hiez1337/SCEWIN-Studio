namespace SCEWIN_Studio.Models;

public class AppSettings
{
    public string ScewinExePath { get; set; } = string.Empty;
    public string Language { get; set; } = "ru-RU";
    public bool DarkTheme { get; set; } = true;
    public bool AutoScanOnStartup { get; set; } = true;
    public string LastDumpDirectory { get; set; } = string.Empty;
    public string BackupDirectory { get; set; } = string.Empty;
}
