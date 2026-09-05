namespace SCEWIN_Studio.Models;

public class ScewinOption
{
    public string ValueHex { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
    public string RawLine { get; set; } = string.Empty;

    public string FormattedDisplayText
    {
        get
        {
            if (!string.IsNullOrEmpty(DisplayText) && DisplayText.EndsWith("h Clk", System.StringComparison.OrdinalIgnoreCase))
            {
                var hexPart = DisplayText.Substring(0, DisplayText.Length - 5).Trim();
                if (int.TryParse(hexPart, System.Globalization.NumberStyles.HexNumber, null, out int dec))
                {
                    return $"{DisplayText} ({dec})";
                }
            }
            return DisplayText;
        }
    }

    public override string ToString() => $"[{ValueHex}] {FormattedDisplayText}";
}

