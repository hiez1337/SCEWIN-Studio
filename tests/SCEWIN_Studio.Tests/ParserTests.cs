using System;
using System.IO;
using System.Linq;
using SCEWIN_Studio.Models;
using SCEWIN_Studio.Services;
using Xunit;

namespace SCEWIN_Studio.Tests;

public class ParserTests
{
    private readonly ScewinParser _parser = new();

    private const string SampleDump = @"// Script File Name : nvram.txt
// Created on 06/19/26 at 15:50:42    
// AMISCE Utility. Ver 5.05.01.0002
// Copyright (c) 2021 AMI. All rights reserved.

HIICrc32= 8F23D09B


Setup Question	= LAN Power Enable
Help String	= Enable or disable LAN Power
Token	=05	// Do NOT change this line
Offset	=13
Width	=01
Options	=[00]Disabled	// Move ""*"" to the desired Option
         *[01]Enabled 

Setup Question	= Wireless LAN Recovery
Help String	= Wireless LAN Recovery suppport Enable/Disable
Token	=08	// Do NOT change this line
Offset	=47
Width	=01
Options	=*[00]Disabled	// Move ""*"" to the desired Option
         [01]Enabled 
         [02]Dummy Reset
";

    [Fact]
    public void Parse_ExtractsHiiCrc32AndVersion()
    {
        var dump = _parser.Parse(SampleDump);

        Assert.Equal("8F23D09B", dump.HiiCrc32);
        Assert.Equal("5.05.01.0002", dump.UtilityVersion);
        Assert.Equal("06/19/26 at 15:50:42", dump.CreatedDate);
        Assert.Equal(2, dump.Tokens.Count);
    }

    [Fact]
    public void Parse_ExtractsTokensAndOptions()
    {
        var dump = _parser.Parse(SampleDump);

        var token1 = dump.Tokens[0];
        Assert.Equal("LAN Power Enable", token1.Question);
        Assert.Equal("05", token1.TokenId);
        Assert.Equal("13", token1.Offset);
        Assert.Equal("01", token1.Width);
        Assert.Equal(2, token1.Options.Count);
        Assert.NotNull(token1.OriginalOption);
        Assert.Equal("01", token1.OriginalOption!.ValueHex);
        Assert.Equal("Enabled", token1.OriginalOption.DisplayText);
        Assert.False(token1.IsModified);

        var token2 = dump.Tokens[1];
        Assert.Equal("Wireless LAN Recovery", token2.Question);
        Assert.Equal("08", token2.TokenId);
        Assert.Equal(3, token2.Options.Count);
        Assert.NotNull(token2.OriginalOption);
        Assert.Equal("00", token2.OriginalOption!.ValueHex);
        Assert.Equal("Disabled", token2.OriginalOption.DisplayText);
    }

    [Fact]
    public void Token_ModificationStateTracksCorrectly()
    {
        var dump = _parser.Parse(SampleDump);
        var token = dump.Tokens[0];

        Assert.False(token.IsModified);

        // Change to option 00 (Disabled)
        token.CurrentOption = token.Options.First(o => o.ValueHex == "00");
        Assert.True(token.IsModified);
        Assert.Equal("Disabled", token.CurrentDisplayValue);

        // Change back to original
        token.CurrentOption = token.Options.First(o => o.ValueHex == "01");
        Assert.False(token.IsModified);

        // Reset method test
        token.CurrentOption = token.Options.First(o => o.ValueHex == "00");
        Assert.True(token.IsModified);
        token.Reset();
        Assert.False(token.IsModified);
    }

    [Fact]
    public void Parse_EmptyAndMalformedInputs_DoesNotThrow()
    {
        var dump1 = _parser.Parse("");
        Assert.NotNull(dump1);
        Assert.Empty(dump1.Tokens);

        var dump2 = _parser.Parse("Random invalid text\nwith garbage\nHIICrc32=ABC123\nSetup Question=");
        Assert.NotNull(dump2);
        Assert.Equal("ABC123", dump2.HiiCrc32);
    }

    [Theory]
    [InlineData("PCIe ASPM Support", "Control ASPM", "ASPM")]
    [InlineData("PCI Express x16 Bifurcation", "Bifurcate x8/x8", "ASPM")]
    [InlineData("PBO Limits", "Precision Boost Overdrive", "Overclocking")]
    [InlineData("Curve Optimizer", "Configure core voltage offsets", "Overclocking")]
    [InlineData("DRAM Frequency", "Memory clock speed", "Memory")]
    [InlineData("CAS Latency", "tCL timing", "Memory")]
    [InlineData("Global C-state Control", "Enable CPU C-States", "CpuPower")]
    [InlineData("Core Performance Boost", "CPB turbo boost", "CpuPower")]
    [InlineData("LAN Power Enable", "Enable onboard LAN", "Peripherals")]
    public void DetermineCategory_CategorizesCorrectly(string question, string help, string expectedCategory)
    {
        var cat = ScewinParser.DetermineCategory(question, help);
        Assert.Equal(expectedCategory, cat);
    }
}
