namespace SCEWIN_Studio.Models;

public class ScewinDiffItem
{
    public string TokenId { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public string Offset { get; set; } = string.Empty;
    public ScewinToken TokenRef { get; set; } = null!;
}
