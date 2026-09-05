using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SCEWIN_Studio.Models;

public enum ComparisonStatus
{
    Equal,
    Different,
    OnlyInA,
    OnlyInB
}

public partial class DumpComparisonItem : ObservableObject
{
    public string Question { get; set; } = string.Empty;
    public string HelpString { get; set; } = string.Empty;
    public string Category { get; set; } = "Other";
    public string SubCategory { get; set; } = "General";

    public string ValueA { get; set; } = "—";
    public string TokenA { get; set; } = "—";
    public string OffsetA { get; set; } = "—";

    public string ValueB { get; set; } = "—";
    public string TokenB { get; set; } = "—";
    public string OffsetB { get; set; } = "—";

    public ComparisonStatus Status { get; set; }

    public bool IsEqual => Status == ComparisonStatus.Equal;
    public bool IsDifferent => Status == ComparisonStatus.Different;
    public bool IsOnlyInA => Status == ComparisonStatus.OnlyInA;
    public bool IsOnlyInB => Status == ComparisonStatus.OnlyInB;

    public string StatusBadgeText => Status switch
    {
        ComparisonStatus.Equal => "Совпадает",
        ComparisonStatus.Different => "Различие",
        ComparisonStatus.OnlyInA => "Только в А",
        ComparisonStatus.OnlyInB => "Только в Б",
        _ => string.Empty
    };

    public string StatusBadgeTextEn => Status switch
    {
        ComparisonStatus.Equal => "Equal",
        ComparisonStatus.Different => "Different",
        ComparisonStatus.OnlyInA => "Only in A",
        ComparisonStatus.OnlyInB => "Only in B",
        _ => string.Empty
    };

    public string StatusColor => Status switch
    {
        ComparisonStatus.Different => "#FF5252",
        ComparisonStatus.Equal => "#4CAF50",
        ComparisonStatus.OnlyInA => "#40A9FF",
        ComparisonStatus.OnlyInB => "#B37FEB",
        _ => "#888888"
    };

    public string StatusBackground => Status switch
    {
        ComparisonStatus.Different => "#321619",
        ComparisonStatus.Equal => "#122B1B",
        ComparisonStatus.OnlyInA => "#11263C",
        ComparisonStatus.OnlyInB => "#241838",
        _ => "#242424"
    };

    public string StatusBorderBrush => Status switch
    {
        ComparisonStatus.Different => "#8A2A2B",
        ComparisonStatus.Equal => "#236B3B",
        ComparisonStatus.OnlyInA => "#1D5D9B",
        ComparisonStatus.OnlyInB => "#6C3A82",
        _ => "#3E3E3E"
    };

    public string ComparisonSymbol => Status switch
    {
        ComparisonStatus.Equal => "=",
        ComparisonStatus.Different => "≠",
        ComparisonStatus.OnlyInA => "←",
        ComparisonStatus.OnlyInB => "→",
        _ => "•"
    };

    public string TokensSummary
    {
        get
        {
            if (IsOnlyInA) return $"Token: 0x{TokenA} | Off: 0x{OffsetA}";
            if (IsOnlyInB) return $"Token: 0x{TokenB} | Off: 0x{OffsetB}";
            if (TokenA == TokenB && OffsetA == OffsetB) return $"Token: 0x{TokenA} | Off: 0x{OffsetA}";
            return $"A: [T:0x{TokenA}, O:0x{OffsetA}] vs B: [T:0x{TokenB}, O:0x{OffsetB}]";
        }
    }
}
