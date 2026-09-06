using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SCEWIN_Studio.Models;

namespace SCEWIN_Studio.Services;

public class ScewinParser : IScewinParser
{
    private static readonly Regex HiiRegex = new(@"HIICrc32\s*=\s*([0-9A-Fa-f]+)", RegexOptions.Compiled);
    private static readonly Regex VerRegex = new(@"AMISCE Utility\.\s*Ver\s*([0-9\.]+)", RegexOptions.Compiled);
    private static readonly Regex DateRegex = new(@"Created on\s*(.+)", RegexOptions.Compiled);
    private static readonly Regex OptionRegex = new(@"^\s*(\*?)\s*\[([0-9A-Fa-f]+)\]\s*(.*?)(?:\s*//.*)?$", RegexOptions.Compiled);

    public ScewinDump Parse(string content, string filePath = "")
    {
        var dump = new ScewinDump { FilePath = filePath };
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        var headerComments = new List<string>();
        int lineIdx = 0;

        // 1. Parse header
        while (lineIdx < lines.Length)
        {
            var line = lines[lineIdx].Trim();
            if (string.IsNullOrEmpty(line))
            {
                lineIdx++;
                continue;
            }

            if (line.StartsWith("//"))
            {
                headerComments.Add(line);
                var verMatch = VerRegex.Match(line);
                if (verMatch.Success) dump.UtilityVersion = verMatch.Groups[1].Value.Trim();

                var dateMatch = DateRegex.Match(line);
                if (dateMatch.Success) dump.CreatedDate = dateMatch.Groups[1].Value.Trim();

                lineIdx++;
                continue;
            }

            var hiiMatch = HiiRegex.Match(line);
            if (hiiMatch.Success)
            {
                dump.HiiCrc32 = hiiMatch.Groups[1].Value.Trim();
                lineIdx++;
                break;
            }

            if (line.StartsWith("Setup Question", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            lineIdx++;
        }
        dump.HeaderComments = headerComments;

        // 2. Parse tokens
        ScewinToken? currentToken = null;
        bool inOptions = false;

        for (; lineIdx < lines.Length; lineIdx++)
        {
            var rawLine = lines[lineIdx];
            var trimmed = rawLine.Trim();

            if (trimmed.StartsWith("// End of", StringComparison.OrdinalIgnoreCase))
            {
                if (currentToken != null)
                {
                    FinalizeToken(currentToken);
                    dump.Tokens.Add(currentToken);
                    currentToken = null;
                }
                inOptions = false;
                continue;
            }

            if (trimmed.StartsWith("Setup Question", StringComparison.OrdinalIgnoreCase))
            {
                if (currentToken != null)
                {
                    FinalizeToken(currentToken);
                    dump.Tokens.Add(currentToken);
                }

                inOptions = false;
                currentToken = new ScewinToken();
                var eqIdx = rawLine.IndexOf('=');
                if (eqIdx >= 0)
                {
                    currentToken.Question = CleanValue(rawLine.Substring(eqIdx + 1));
                }
                continue;
            }

            if (currentToken == null) continue;

            if (trimmed.StartsWith("Help String", StringComparison.OrdinalIgnoreCase))
            {
                inOptions = false;
                var eqIdx = rawLine.IndexOf('=');
                if (eqIdx >= 0) currentToken.HelpString = CleanValue(rawLine.Substring(eqIdx + 1));
                continue;
            }

            if (trimmed.StartsWith("Token", StringComparison.OrdinalIgnoreCase) && rawLine.Contains('='))
            {
                inOptions = false;
                var eqIdx = rawLine.IndexOf('=');
                if (eqIdx >= 0) currentToken.TokenId = CleanValue(rawLine.Substring(eqIdx + 1));
                continue;
            }

            if (trimmed.StartsWith("Offset", StringComparison.OrdinalIgnoreCase) && rawLine.Contains('='))
            {
                inOptions = false;
                var eqIdx = rawLine.IndexOf('=');
                if (eqIdx >= 0) currentToken.Offset = CleanValue(rawLine.Substring(eqIdx + 1));
                continue;
            }

            if (trimmed.StartsWith("Width", StringComparison.OrdinalIgnoreCase) && rawLine.Contains('='))
            {
                inOptions = false;
                var eqIdx = rawLine.IndexOf('=');
                if (eqIdx >= 0) currentToken.Width = CleanValue(rawLine.Substring(eqIdx + 1));
                continue;
            }

            if (trimmed.StartsWith("Value", StringComparison.OrdinalIgnoreCase) && rawLine.Contains('='))
            {
                inOptions = false;
                var eqIdx = rawLine.IndexOf('=');
                if (eqIdx >= 0)
                {
                    var val = CleanValue(rawLine.Substring(eqIdx + 1));
                    if (val.StartsWith("<") && val.EndsWith(">"))
                    {
                        currentToken.NumericHasAngleBrackets = true;
                        val = val.Substring(1, val.Length - 2).Trim();
                    }
                    currentToken.OriginalNumericValue = val;
                    currentToken.CustomNumericValue = val;
                }
                continue;
            }

            if (trimmed.StartsWith("Options", StringComparison.OrdinalIgnoreCase))
            {
                inOptions = true;
                var eqIdx = rawLine.IndexOf('=');
                if (eqIdx >= 0)
                {
                    var optContent = rawLine.Substring(eqIdx + 1);
                    ParseAndAddOption(currentToken, optContent, rawLine);
                }
                continue;
            }

            if (inOptions)
            {
                if (string.IsNullOrWhiteSpace(rawLine))
                {
                    inOptions = false;
                    continue;
                }

                if (trimmed.Contains('[') && trimmed.Contains(']'))
                {
                    ParseAndAddOption(currentToken, rawLine, rawLine);
                    continue;
                }
                else
                {
                    inOptions = false;
                }
            }
        }

        if (currentToken != null)
        {
            FinalizeToken(currentToken);
            dump.Tokens.Add(currentToken);
        }

        return dump;
    }

    private void ParseAndAddOption(ScewinToken token, string lineContent, string rawLine)
    {
        // High-speed fast path for standard option lines: *[00]Disabled or [01]Enabled
        int commentIdx = lineContent.IndexOf("//", StringComparison.Ordinal);
        string content = commentIdx >= 0 ? lineContent.Substring(0, commentIdx) : lineContent;

        int openBracket = content.IndexOf('[');
        int closeBracket = openBracket >= 0 ? content.IndexOf(']', openBracket + 1) : -1;
        if (openBracket >= 0 && closeBracket > openBracket)
        {
            var prefix = content.Substring(0, openBracket);
            int starCount = 0;
            bool validPrefix = true;
            for (int i = 0; i < prefix.Length; i++)
            {
                char c = prefix[i];
                if (c == '*') starCount++;
                else if (c != ' ' && c != '\t')
                {
                    validPrefix = false;
                    break;
                }
            }

            if (validPrefix && starCount <= 1)
            {
                string hexVal = content.Substring(openBracket + 1, closeBracket - openBracket - 1).Trim();
                string text = content.Substring(closeBracket + 1).Trim();

                bool isValidHex = hexVal.Length > 0;
                for (int i = 0; i < hexVal.Length; i++)
                {
                    char c = hexVal[i];
                    if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    {
                        isValidHex = false;
                        break;
                    }
                }

                if (isValidHex)
                {
                    bool isSelected = starCount == 1;
                    var opt = new ScewinOption
                    {
                        ValueHex = hexVal,
                        DisplayText = string.IsNullOrEmpty(text) ? hexVal : text,
                        IsSelected = isSelected,
                        RawLine = rawLine
                    };

                    token.Options.Add(opt);
                    if (isSelected)
                    {
                        token.OriginalOption = opt;
                        token.CurrentOption = opt;
                    }
                    return;
                }
            }
        }

        var match = OptionRegex.Match(lineContent);
        if (match.Success)
        {
            bool isSelected = match.Groups[1].Value == "*";
            string hexVal = match.Groups[2].Value.Trim();
            string text = match.Groups[3].Value.Trim();

            var opt = new ScewinOption
            {
                ValueHex = hexVal,
                DisplayText = string.IsNullOrEmpty(text) ? hexVal : text,
                IsSelected = isSelected,
                RawLine = rawLine
            };

            token.Options.Add(opt);
            if (isSelected)
            {
                token.OriginalOption = opt;
                token.CurrentOption = opt;
            }
        }
    }

    private void FinalizeToken(ScewinToken token)
    {
        if (token.OriginalOption == null && token.Options.Count > 0)
        {
            token.OriginalOption = token.Options.FirstOrDefault();
            token.CurrentOption = token.OriginalOption;
        }

        // Categorization
        token.Category = DetermineCategory(token.Question, token.HelpString);
        token.SubCategory = DetermineSubCategory(token.Question, token.HelpString, token.Category);
        token.SafetyLevel = DetermineSafety(token.Question, token.HelpString, token.Category);
        token.MemoryTier = DetermineMemoryTier(token);

        if (token.MemoryTier != MemoryTier.None && token.Category != "Memory")
        {
            token.Category = "Memory";
            token.SubCategory = DetermineSubCategory(token.Question, token.HelpString, token.Category);
        }
    }

    private static readonly HashSet<string> MemoryTimingExactNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Tcl", "Trcdrd", "Trcdwr", "Trp", "Tras", "Trc", "Trc Ctrl", "TrrdS", "TrrdL",
        "Tfaw", "Tfaw Ctrl", "TwtrS", "TwtrL", "Twr", "Trcpage",
        "TrdrdSc", "TrdrdScl", "TrdrdSd", "TrdrdDd",
        "TwrwrSc", "TwrwrScl", "TwrwrSd", "TwrwrDd",
        "Trdwr", "Twrrd", "Trcpb", "Trfc", "Trfc2", "Trfc4", "Trfc Ctrl",
        "ProcODT", "RttNom", "RttWr", "RttPark", "CsOdt",
        "tCL", "tRCDRD", "tRCDWR", "tRP", "tRAS", "tRC", "tFAW", "tRRDS", "tRRDL",
        "tRRD_S", "tRRD_L", "tWTRS", "tWTRL", "tWTR_S", "tWTR_L", "tWR",
        "tRDRD_SC", "tRDRD_SD", "tRDRD_DD", "tRDRD_SCL",
        "tWRWR_SC", "tWRWR_SD", "tWRWR_DD", "tWRWR_SCL", "tRDWR", "tWRRD",
        "tRFC", "tRFC1", "tRFC2", "tRFC4", "tCWL", "tCKE", "tREFI", "Cmd2T", "Gear Down Mode", "Power Down Enable"
    };

    public static bool IsMemoryTimingQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question)) return false;
        var clean = question.Trim();
        var multiSpaceIdx = clean.IndexOf("  ", StringComparison.Ordinal);
        if (multiSpaceIdx > 0)
        {
            clean = clean.Substring(0, multiSpaceIdx).Trim();
        }

        if (MemoryTimingExactNames.Contains(clean)) return true;

        var q = clean.ToLowerInvariant();
        if (q.StartsWith("tcl") || q.StartsWith("trcd") || q.StartsWith("trp") || 
            q.StartsWith("tras") || q.StartsWith("trc") || q.StartsWith("trrd") || 
            q.StartsWith("tfaw") || q.StartsWith("twtr") || q.StartsWith("twr") || 
            q.StartsWith("trfc") || q.StartsWith("trdrd") || q.StartsWith("twrwr") || 
            q.StartsWith("trdwr") || q.StartsWith("twrrd") || q.StartsWith("tcwl") ||
            q.StartsWith("tcke") || q.StartsWith("procodt") || q.StartsWith("rttnom") ||
            q.StartsWith("rttwr") || q.StartsWith("rttpark"))
        {
            return true;
        }

        return false;
    }

    public static MemoryTier DetermineMemoryTier(ScewinToken token)
    {
        var q = token.Question?.Trim() ?? string.Empty;
        var id = token.TokenId?.Trim().ToUpperInvariant() ?? string.Empty;
        var offset = token.Offset?.Trim().ToUpperInvariant() ?? string.Empty;

        // Gate: The token MUST be memory-related (timing question, memory category, or DRAM clock options)
        bool isTiming = IsMemoryTimingQuestion(q);
        bool hasDramClkOptions = token.Options.Any(o => o.DisplayText.Contains("Clk", StringComparison.OrdinalIgnoreCase));
        bool isMemoryRelated = token.Category == "Memory" || isTiming || hasDramClkOptions;

        if (!isMemoryRelated)
        {
            return MemoryTier.None;
        }

        if (int.TryParse(id, System.Globalization.NumberStyles.HexNumber, null, out int tokenNum))
        {
            // Tier 2: MSI Click BIOS OC Engine (Tokens 0x2A28..0x2A2C, Setup VarStore)
            if ((tokenNum >= 0x2A28 && tokenNum <= 0x2A65) ||
                (tokenNum >= 0x2AE1 && tokenNum <= 0x2AE5) ||
                (tokenNum >= 0x2B64 && tokenNum <= 0x2B85))
            {
                if (isTiming || token.Category == "Memory")
                {
                    return MemoryTier.Tier2Msi;
                }
            }
            if (id.StartsWith("2A2") || id.StartsWith("2A3") || id.StartsWith("2A4") || id.StartsWith("2A5") || id.StartsWith("2AE") || id.StartsWith("2B6") || id.StartsWith("2B7"))
            {
                if (isTiming || q.StartsWith("tCL", StringComparison.OrdinalIgnoreCase) || q.StartsWith("tRCD", StringComparison.OrdinalIgnoreCase) || q.StartsWith("tRP", StringComparison.OrdinalIgnoreCase) || q.StartsWith("tRAS", StringComparison.OrdinalIgnoreCase))
                {
                    return MemoryTier.Tier2Msi;
                }
            }

            // Tier 3: AMD Overclocking PBS (Tokens 0x64..0x68, service duplicate menu in Auto)
            if (tokenNum >= 0x64 && tokenNum <= 0x68)
            {
                if (isTiming || offset.StartsWith("D") || offset.StartsWith("E") || offset.StartsWith("F") || offset.StartsWith("10"))
                {
                    return MemoryTier.Tier3Pbs;
                }
            }
            if (tokenNum >= 0x62 && tokenNum <= 0x8A)
            {
                if (offset.StartsWith("D") || offset.StartsWith("E") || offset.StartsWith("F") || offset.StartsWith("10") || (isTiming && !offset.StartsWith("4") && !offset.StartsWith("5") && !offset.StartsWith("6") && !offset.StartsWith("7")))
                {
                    return MemoryTier.Tier3Pbs;
                }
            }

            // Tier 1: AMD CBS (Tokens 0x2C..0x30, AmdSetup VarStore hardware AGESA registers)
            if (tokenNum >= 0x2C && tokenNum <= 0x30)
            {
                if (isTiming && (offset.StartsWith("4") || offset.Length == 0 || hasDramClkOptions))
                {
                    return MemoryTier.Tier1Cbs;
                }
                else if (offset.StartsWith("4") || offset.StartsWith("5"))
                {
                    return MemoryTier.Tier1Cbs;
                }
            }
            if (tokenNum >= 0x29 && tokenNum <= 0x57)
            {
                if (offset.StartsWith("4") || offset.StartsWith("5") || offset.StartsWith("6") || offset.StartsWith("7") || (isTiming && hasDramClkOptions))
                {
                    return MemoryTier.Tier1Cbs;
                }
            }
        }

        if (id.StartsWith("2A28") || id.StartsWith("2A29") || id.StartsWith("2A2A") || id.StartsWith("2A2B") || id.StartsWith("2A2C"))
        {
            if (isTiming || token.Category == "Memory")
            {
                return MemoryTier.Tier2Msi;
            }
        }

        return MemoryTier.None;
    }

    private static string CleanValue(string raw)
    {
        for (int i = 0; i < raw.Length - 1; i++)
        {
            if (raw[i] == '/' && raw[i + 1] == '/')
            {
                if (i == 0 || char.IsWhiteSpace(raw[i - 1]))
                {
                    raw = raw.Substring(0, i);
                    break;
                }
            }
        }
        return raw.Trim();
    }

    public static string DetermineCategory(string question, string help)
    {
        var q = (question + " " + help).ToLowerInvariant();
        var qName = question.Trim().ToLowerInvariant();

        // 1. Boot & Security (Fast Boot, Secure Boot, TPM, SATA/RAID)
        // Must NEVER leak into CPU Frequency or Overclocking
        if (q.Contains("fast boot") || q.Contains("boot mode") || q.Contains("boot option") ||
            q.Contains("csm support") || q.Contains("launch csm") || q.Contains("quiet boot") ||
            q.Contains("post delay") || q.Contains("numlock") || q.Contains("ftpm") ||
            q.Contains("security device") || q.Contains("trusted computing") || q.Contains("secure boot") ||
            q.Contains("chassis intrusion") || q.Contains("sata mode") || q.Contains("nvme raid") ||
            q.Contains("raidxpert") || q.Contains("sata controller") || q.Contains("ahci"))
        {
            return "BootSecurity";
        }

        // 2. Peripherals (Thunderbolt, USB, Network, Audio, RGB)
        // Must NEVER leak into Fans / Power (even if help string says "Wake Up Command" or "Power")
        if (q.Contains("thunderbolt") || q.Contains("usb") || q.Contains("xhci") || q.Contains("ehci") ||
            q.Contains("type-c") || q.Contains("wlan") || q.Contains("wifi") || q.Contains("wi-fi") ||
            q.Contains("bluetooth") || q.Contains("audio") || q.Contains("azalia") || q.Contains("rgb") ||
            q.Contains("mystic light") || q.Contains("pldr") ||
            Regex.IsMatch(q, @"\blan\b", RegexOptions.IgnoreCase))
        {
            return "Peripherals";
        }

        // 3. PCIe & Bus Subsystem (ASPM, Bifurcation, Lanes, ReBAR)
        if (q.Contains("aspm") || q.Contains("bifurcation") || q.Contains("pcie") || q.Contains("pci express") ||
            q.Contains("peg ") || q.Contains("link state") || q.Contains("l0s") || q.Contains("l1 entry") ||
            q.Contains("link width") || q.Contains("re-size bar") || q.Contains("resize bar") || q.Contains("rebar") ||
            q.Contains("above 4g") || q.Contains("smart access memory") || q.Contains("primary video") || q.Contains("discrete gpu"))
        {
            return "ASPM";
        }

        // 4. DRAM & Memory (Timings, Voltages, Resistances, Frequencies)
        // Must NEVER leak into CPU Overclocking!
        if (IsMemoryTimingQuestion(question) ||
            q.Contains("dram") || q.Contains("memory") || q.Contains("cas latency") ||
            q.Contains("procodt") || q.Contains("rttnom") || q.Contains("rttwr") || q.Contains("rttpark") ||
            q.Contains("csodt") || q.Contains("cad bus") || q.Contains("gear down mode") ||
            q.Contains("power down enable") || q.Contains("cmd2t") || q.Contains("command rate") ||
            q.Contains("fclk") || q.Contains("infinity fabric") || q.Contains("uclk") || q.Contains("mclk") ||
            q.Contains("a-xmp") || q.Contains("expo") || q.Contains("vddio") || q.Contains("vddp") ||
            q.Contains("soc voltage") || q.Contains("dram voltage") || q.Contains("vddcr_soc") ||
            q.Contains("interleaving"))
        {
            return "Memory";
        }

        // 5. Cooling & Fans (Fan profiles, thermal throttling, VRM switching)
        if (q.Contains("fan ") || q.Contains("fan mode") || q.Contains("fan control") || q.Contains("fan curve") ||
            q.Contains("fan step") || q.Contains("system fan") || q.Contains("cpu fan") || q.Contains("pump fan") ||
            q.Contains("smart fan") || q.Contains("tjmax") || q.Contains("thermal throttle") ||
            q.Contains("temperature source") || q.Contains("vrm switching") || q.Contains("vrm spread"))
        {
            return "CoolingPower";
        }

        // 6. CPU Overclocking & PBO
        if (q.Contains("pbo") || q.Contains("precision boost") || q.Contains("curve optimizer") ||
            q.Contains("scalar") || q.Contains("ppt limit") || q.Contains("tdc limit") ||
            q.Contains("edc limit") || q.Contains("boost clock") || q.Contains("cpu ratio") ||
            q.Contains("core ratio") || q.Contains("vcore"))
        {
            return "Overclocking";
        }

        // 7. CPU Power & Sleep States (C-States, CPPC, CPB, SMT, Topology)
        if (q.Contains("c-state") || q.Contains("cstate") || q.Contains("global c-state") ||
            q.Contains("df c-state") || q.Contains("cpb") || q.Contains("core performance boost") ||
            q.Contains("pss") || q.Contains("cool'n'quiet") || q.Contains("smt") || q.Contains("svm") ||
            q.Contains("cppc") || q.Contains("downcore") || q.Contains("ccd control") ||
            q.Contains("processor cores") || q.Contains("power supply idle"))
        {
            return "CpuPower";
        }

        return "Other";
    }

    public static string DetermineSubCategory(string question, string help, string category)
    {
        var q = (question + " " + help).ToLowerInvariant();
        var qName = question.Trim().ToLowerInvariant();

        // 1. Memory Subcategories
        if (category == "Memory" || IsMemoryTimingQuestion(question))
        {
            // Primary Timings: tCL, tRCDRD, tRCDWR, tRP, tRAS, tRC, CAS Latency
            if (qName.StartsWith("tcl") || qName.StartsWith("cas latency") ||
                qName.StartsWith("trcdrd") || qName.StartsWith("trcdwr") ||
                qName.StartsWith("trp") || qName.StartsWith("tras") ||
                (qName.StartsWith("trc") && !qName.StartsWith("trcpb") && !qName.StartsWith("trcpage") && !qName.StartsWith("trcd")) ||
                q.Contains("cas latency (tcl)") || q.Contains("ras# to cas#") || q.Contains("row precharge"))
            {
                return "PrimaryTimings";
            }

            // Tertiary Timings: tRDRD, tWRWR, tRDWR, tWRRD, tRCpage, tRCpb
            if (q.Contains("trdrd") || q.Contains("twrwr") || q.Contains("trdwr") ||
                q.Contains("twrrd") || q.Contains("trcpage") || q.Contains("trcpb"))
            {
                return "TertiaryTimings";
            }

            // Terminations & GDM: ProcODT, Rtt, CAD Bus, GDM, PDE, Cmd2T, tRFC, tREFI
            if (q.Contains("procodt") || q.Contains("rttnom") || q.Contains("rttwr") ||
                q.Contains("rttpark") || q.Contains("csodt") || q.Contains("cad bus") ||
                q.Contains("gear down mode") || q.Contains("power down enable") ||
                q.Contains("cmd2t") || q.Contains("command rate") || q.Contains("trfc") ||
                q.Contains("trefi") || q.Contains("tcke") || q.Contains("drv"))
            {
                return "TerminationsGdm";
            }

            // Secondary Timings: tFAW, tRRD, tWTR, tWR, tCWL, tRTP
            if (q.Contains("tfaw") || q.Contains("trrd") || q.Contains("twtr") ||
                qName.StartsWith("twr") || Regex.IsMatch(q, @"\btwr\b") || q.Contains("tcwl") || q.Contains("trtp") ||
                q.Contains("write recovery") || q.Contains("four activate"))
            {
                return "SecondaryTimings";
            }

            // Voltages: DRAM Voltage, VDDIO, VDDP, SoC Voltage
            if (q.Contains("voltage") || q.Contains("vddio") || q.Contains("vddp") ||
                q.Contains("soc voltage") || q.Contains("vddcr") || q.Contains("vpp") ||
                q.Contains("dram vdd"))
            {
                return "Voltages";
            }

            // Frequency & Fabric: FCLK, UCLK, MCLK, Frequency, Interleaving, A-XMP, EXPO
            if (q.Contains("frequency") || q.Contains("clock") || q.Contains("fclk") ||
                q.Contains("uclk") || q.Contains("mclk") || q.Contains("fabric") ||
                q.Contains("interleaving") || q.Contains("xmp") || q.Contains("expo") ||
                q.Contains("multiplier"))
            {
                return "Frequency";
            }

            return "PrimaryTimings";
        }

        // 2. PCIe Subcategories
        if (category == "ASPM")
        {
            // ReBar: Re-Size BAR, Above 4G, SAM, Primary Video
            if (q.Contains("re-size") || q.Contains("resize") || q.Contains("rebar") ||
                q.Contains("above 4g") || q.Contains("bar support") ||
                q.Contains("smart access memory") || q.Contains("primary video") ||
                q.Contains("gpu hotplug"))
            {
                return "ReBar";
            }

            // Bifurcation & Speed: Bifurcation, Lanes configuration, Link speed, Gen1..5
            if (q.Contains("bifurcation") || q.Contains("lanes configuration") ||
                q.Contains("link speed") || q.Contains("pcie speed") ||
                q.Contains("gen1") || q.Contains("gen2") || q.Contains("gen3") ||
                q.Contains("gen4") || q.Contains("gen5") || q.Contains("link width") ||
                q.Contains("chipset link"))
            {
                return "BifurcationSpeed";
            }

            // ASPM & L1: ASPM Support, PM L1 SS, L0s, L1 entry
            if (q.Contains("aspm") || q.Contains("l1") || q.Contains("l0s") ||
                q.Contains("link state") || q.Contains("active state power"))
            {
                return "AspmL1";
            }

            return "AspmL1";
        }

        // 3. CPU Overclocking & CPU Power Subcategories
        if (category == "Overclocking" || category == "CpuPower")
        {
            // PBO Limits: PPT, TDC, EDC, Platform Power
            if (q.Contains("ppt limit") || q.Contains("tdc limit") || q.Contains("edc limit") ||
                q.Contains("power limit") || q.Contains("package power") || q.Contains("platform power"))
            {
                return "PboLimits";
            }

            // PBO & Curve Optimizer: Curve Optimizer, PBO, Boost clock override, Scalar, CPB
            if (q.Contains("curve optimizer") || q.Contains("pbo") || q.Contains("precision boost") ||
                q.Contains("boost clock") || q.Contains("scalar") || q.Contains("cpb") ||
                q.Contains("core performance boost") || q.Contains("max boost") ||
                q.Contains("cpu ratio") || q.Contains("core ratio") || q.Contains("vcore"))
            {
                return "PboCurve";
            }

            // CPU Topology: SMT, CCD Control, Core Control, Downcore, Virtualization / SVM
            if (q.Contains("smt") || q.Contains("simultaneous multithreading") ||
                q.Contains("ccd control") || q.Contains("core control") ||
                q.Contains("downcore") || q.Contains("active processor cores") ||
                q.Contains("svm") || q.Contains("virtualization") || q.Contains("nx bit"))
            {
                return "CpuTopology";
            }

            // CPU Power States: C-States, DF C-States, CPPC, PSS, Cool'n'Quiet, Power supply idle
            if (q.Contains("c-state") || q.Contains("cstate") || q.Contains("df c-state") ||
                q.Contains("global c-state") || q.Contains("cppc") || q.Contains("preferred cores") ||
                q.Contains("pss") || q.Contains("cool'n'quiet") || q.Contains("idle control") ||
                q.Contains("power supply idle") || q.Contains("core c6"))
            {
                return "CpuPowerStates";
            }

            return category == "Overclocking" ? "PboCurve" : "CpuPowerStates";
        }

        // 4. Cooling & Power Subcategories
        if (category == "CoolingPower")
        {
            if (q.Contains("thermal") || q.Contains("tjmax") || q.Contains("throttle") ||
                q.Contains("temperature source"))
            {
                return "ThermalLimits";
            }

            if (q.Contains("vrm") || q.Contains("switching frequency") || q.Contains("phase") ||
                q.Contains("spread spectrum"))
            {
                return "VrmPower";
            }

            return "FanProfiles";
        }

        // 5. Peripherals Subcategories
        if (category == "Peripherals")
        {
            if (q.Contains("usb") || q.Contains("thunderbolt") || q.Contains("xhci") ||
                q.Contains("ehci") || q.Contains("type-c"))
            {
                return "UsbThunderbolt";
            }

            if (q.Contains("audio") || q.Contains("azalia") || q.Contains("sound") ||
                q.Contains("rgb") || q.Contains("led") || q.Contains("mystic light"))
            {
                return "AudioRgb";
            }

            if (Regex.IsMatch(q, @"\blan\b", RegexOptions.IgnoreCase) || q.Contains("wlan") ||
                q.Contains("wifi") || q.Contains("wi-fi") || q.Contains("bluetooth") ||
                q.Contains("network") || q.Contains("ethernet") || q.Contains("pldr"))
            {
                return "Network";
            }

            return "Network";
        }

        // 6. Boot & Security Subcategories
        if (category == "BootSecurity")
        {
            if (q.Contains("ftpm") || q.Contains("security device") || q.Contains("trusted computing") ||
                q.Contains("secure boot") || q.Contains("chassis intrusion") || q.Contains("tpm"))
            {
                return "SecurityTpm";
            }

            if (q.Contains("sata") || q.Contains("ahci") || q.Contains("raid") ||
                q.Contains("nvme raid") || q.Contains("raidxpert"))
            {
                return "StorageRaid";
            }

            if (q.Contains("boot") || q.Contains("csm") || q.Contains("post delay") ||
                q.Contains("numlock") || q.Contains("quiet boot"))
            {
                return "BootParams";
            }

            return "BootParams";
        }

        return "General";
    }

    public static string DetermineSafety(string question, string help, string category)
    {
        var q = (question + " " + help).ToLowerInvariant();

        if (q.Contains("voltage") || q.Contains("vdd") || q.Contains("vcore") || q.Contains("soc voltage") || q.Contains("procodt") || q.Contains("dram voltage"))
            return "Warning";

        if (category == "Overclocking" || category == "Memory" || q.Contains("bifurcation") || q.Contains("frequency") || q.Contains("multiplier"))
            return "Advanced";

        return "Safe";
    }

    public string GenerateDiffScript(ScewinDump dump, IEnumerable<ScewinToken> modifiedTokens)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// Script File Name : nvram_diff.txt");
        sb.AppendLine($"// Generated by SCEWIN Studio on {DateTime.Now:MM/dd/yy} at {DateTime.Now:HH:mm:ss}");
        sb.AppendLine($"// AMISCE Utility. {(string.IsNullOrEmpty(dump.UtilityVersion) ? "Ver 5.05.01.0002" : dump.UtilityVersion)}");
        sb.AppendLine("// Copyright (c) 2021 AMI. All rights reserved.");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(dump.HiiCrc32))
        {
            sb.AppendLine($"HIICrc32= {dump.HiiCrc32}");
            sb.AppendLine();
        }

        foreach (var token in modifiedTokens)
        {
            sb.AppendLine();
            sb.AppendLine($"Setup Question\t= {token.Question}");
            if (!string.IsNullOrEmpty(token.HelpString))
            {
                sb.AppendLine($"Help String\t= {token.HelpString}");
            }
            sb.AppendLine($"Token\t={token.TokenId}\t// Do NOT change this line");
            sb.AppendLine($"Offset\t={token.Offset}");
            sb.AppendLine($"Width\t={token.Width}");

            if (token.HasOptions)
            {
                sb.Append("Options\t=");
                for (int i = 0; i < token.Options.Count; i++)
                {
                    var opt = token.Options[i];
                    bool isSelected = token.CurrentOption != null &&
                                      string.Equals(opt.ValueHex, token.CurrentOption.ValueHex, StringComparison.OrdinalIgnoreCase);

                    string star = isSelected ? "*" : "";
                    string indent = i == 0 ? "" : "         ";
                    string comment = i == 0 ? "\t// Move \"*\" to the desired Option" : "";

                    sb.AppendLine($"{indent}{star}[{opt.ValueHex}]{opt.DisplayText}{comment}");
                }
            }
            else
            {
                var val = token.CustomNumericValue ?? token.OriginalNumericValue;
                if (token.NumericHasAngleBrackets && !val.StartsWith("<"))
                {
                    val = $"<{val}>";
                }
                sb.AppendLine($"Value\t={val}");
            }
        }

        return sb.ToString();
    }

    public string GenerateFullScript(ScewinDump dump)
    {
        return GenerateDiffScript(dump, dump.Tokens);
    }
}
