using System;
using System.Collections.Generic;

namespace SCEWIN_Studio.Models;

public class ScewinDump
{
    public string FilePath { get; set; } = string.Empty;
    public string? HiiCrc32 { get; set; }
    public string? UtilityVersion { get; set; }
    public string? CreatedDate { get; set; }
    public List<string> HeaderComments { get; set; } = new();
    public List<ScewinToken> Tokens { get; set; } = new();
    public DateTime LoadedAt { get; set; } = DateTime.Now;

    public int TotalTokens => Tokens.Count;
}
