namespace SCEWIN_Studio.Models;

public class DetectionResult
{
    public bool Found { get; set; }
    public string ExePath { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool HasAmiFldrv { get; set; }
    public bool HasAmiGendrv { get; set; }
    public bool DriversReady => HasAmiFldrv && HasAmiGendrv;
    public string Details { get; set; } = string.Empty;
}
