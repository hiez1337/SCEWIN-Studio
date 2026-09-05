using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using SCEWIN_Studio.Models;

namespace SCEWIN_Studio.Services;

public class DumpComparer : IDumpComparer
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public List<DumpComparisonItem> Compare(ScewinDump dumpA, ScewinDump dumpB)
    {
        var results = new List<DumpComparisonItem>();

        var tokensA = dumpA?.Tokens ?? new List<ScewinToken>();
        var tokensB = dumpB?.Tokens ?? new List<ScewinToken>();

        // Group tokens by normalized question name (case-insensitive)
        var groupsA = tokensA
            .GroupBy(t => NormalizeQuestion(t.Question), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var groupsB = tokensB
            .GroupBy(t => NormalizeQuestion(t.Question), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // All distinct normalized question keys preserving order of appearance in Dump A then Dump B
        var allKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tokensA)
        {
            var key = NormalizeQuestion(t.Question);
            if (!string.IsNullOrEmpty(key)) allKeys.Add(key);
        }
        foreach (var t in tokensB)
        {
            var key = NormalizeQuestion(t.Question);
            if (!string.IsNullOrEmpty(key)) allKeys.Add(key);
        }

        foreach (var key in allKeys)
        {
            groupsA.TryGetValue(key, out var listA);
            groupsB.TryGetValue(key, out var listB);

            listA ??= new List<ScewinToken>();
            listB ??= new List<ScewinToken>();

            var pairs = MatchTokenPairs(listA, listB);

            foreach (var (a, b) in pairs)
            {
                var item = CreateComparisonItem(a, b);
                results.Add(item);
            }
        }

        // Sort results: Different first, then OnlyInA / OnlyInB, then Equal.
        // Secondary sort by Category, then Question name.
        return results
            .OrderBy(GetStatusSortOrder)
            .ThenBy(i => i.Category ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Question ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int GetStatusSortOrder(DumpComparisonItem item) => item.Status switch
    {
        ComparisonStatus.Different => 0,
        ComparisonStatus.OnlyInA => 1,
        ComparisonStatus.OnlyInB => 2,
        ComparisonStatus.Equal => 3,
        _ => 4
    };

    private static List<(ScewinToken? A, ScewinToken? B)> MatchTokenPairs(List<ScewinToken> listA, List<ScewinToken> listB)
    {
        var pairs = new List<(ScewinToken? A, ScewinToken? B)>();
        var remainingB = new List<ScewinToken>(listB);

        foreach (var a in listA)
        {
            ScewinToken? matchedB = null;

            // Try to match by SubCategory if specified and distinct
            if (!string.IsNullOrEmpty(a.SubCategory) && !string.Equals(a.SubCategory, "General", StringComparison.OrdinalIgnoreCase))
            {
                matchedB = remainingB.FirstOrDefault(b => string.Equals(b.SubCategory, a.SubCategory, StringComparison.OrdinalIgnoreCase));
            }

            // If not found by SubCategory, match sequentially with next available token in B
            if (matchedB == null && remainingB.Count > 0)
            {
                matchedB = remainingB[0];
            }

            if (matchedB != null)
            {
                pairs.Add((a, matchedB));
                remainingB.Remove(matchedB);
            }
            else
            {
                pairs.Add((a, null));
            }
        }

        // Remaining unmatched in B are OnlyInB
        foreach (var b in remainingB)
        {
            pairs.Add((null, b));
        }

        return pairs;
    }

    private static DumpComparisonItem CreateComparisonItem(ScewinToken? a, ScewinToken? b)
    {
        var item = new DumpComparisonItem();

        if (a != null && b == null)
        {
            item.Status = ComparisonStatus.OnlyInA;
            item.Question = a.DisplayTitle;
            item.HelpString = a.HelpString;
            item.Category = a.Category;
            item.SubCategory = a.SubCategory;
            item.ValueA = a.CurrentDisplayValue;
            item.TokenA = a.TokenId;
            item.OffsetA = a.Offset;
            item.ValueB = "—";
            item.TokenB = "—";
            item.OffsetB = "—";
            return item;
        }

        if (a == null && b != null)
        {
            item.Status = ComparisonStatus.OnlyInB;
            item.Question = b.DisplayTitle;
            item.HelpString = b.HelpString;
            item.Category = b.Category;
            item.SubCategory = b.SubCategory;
            item.ValueA = "—";
            item.TokenA = "—";
            item.OffsetA = "—";
            item.ValueB = b.CurrentDisplayValue;
            item.TokenB = b.TokenId;
            item.OffsetB = b.Offset;
            return item;
        }

        if (a != null && b != null)
        {
            item.Question = !string.IsNullOrEmpty(a.DisplayTitle) ? a.DisplayTitle : b.DisplayTitle;
            item.HelpString = !string.IsNullOrEmpty(a.HelpString) ? a.HelpString : b.HelpString;
            item.Category = !string.IsNullOrEmpty(a.Category) && a.Category != "Other" ? a.Category : b.Category;
            item.SubCategory = !string.IsNullOrEmpty(a.SubCategory) && a.SubCategory != "General" ? a.SubCategory : b.SubCategory;

            item.ValueA = a.CurrentDisplayValue;
            item.TokenA = a.TokenId;
            item.OffsetA = a.Offset;

            item.ValueB = b.CurrentDisplayValue;
            item.TokenB = b.TokenId;
            item.OffsetB = b.Offset;

            bool isEqual = AreTokensEqual(a, b);
            item.Status = isEqual ? ComparisonStatus.Equal : ComparisonStatus.Different;
        }

        return item;
    }

    private static bool AreTokensEqual(ScewinToken a, ScewinToken b)
    {
        var valA = a.CurrentDisplayValue?.Trim() ?? string.Empty;
        var valB = b.CurrentDisplayValue?.Trim() ?? string.Empty;

        // Exact display string match (case-insensitive)
        if (string.Equals(valA, valB, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Selected option display text or hex match
        if (a.HasOptions && b.HasOptions && a.CurrentOption != null && b.CurrentOption != null)
        {
            var optA = a.CurrentOption.DisplayText?.Trim() ?? string.Empty;
            var optB = b.CurrentOption.DisplayText?.Trim() ?? string.Empty;
            if (string.Equals(optA, optB, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(a.CurrentOption.ValueHex) &&
                string.Equals(a.CurrentOption.ValueHex.Trim(), b.CurrentOption.ValueHex.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Numeric equivalence (e.g. "0x0" vs "0", "01" vs "1")
        if (!a.HasOptions && !b.HasOptions)
        {
            var cleanA = valA.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? valA.Substring(2) : valA;
            var cleanB = valB.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? valB.Substring(2) : valB;

            if (long.TryParse(cleanA, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexA) &&
                long.TryParse(cleanB, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexB))
            {
                if (hexA == hexB) return true;
            }

            if (long.TryParse(valA, NumberStyles.Integer, CultureInfo.InvariantCulture, out var decA) &&
                long.TryParse(valB, NumberStyles.Integer, CultureInfo.InvariantCulture, out var decB))
            {
                if (decA == decB) return true;
            }
        }

        return false;
    }

    private static string NormalizeQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question)) return string.Empty;
        return WhitespaceRegex.Replace(question.Trim(), " ");
    }
}
