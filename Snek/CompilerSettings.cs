using Spectre.Console.Cli;
using System.ComponentModel;

namespace Snek;

public class CompilerSettings : CommandSettings
{
    [CommandArgument(0, "<INPUT>")]
    [Description("Path to the input .snek file")]
    public required string InputPath { get; set; }

    [CommandOption("-o|--output <OUTPUT>")]
    [Description("Specify output file (default: output.asm or output.exe)")]
    public string? OutputPath { get; set; }

    [CommandOption("--syntax <SYNTAX>")]
    [Description("Use alternate syntax: python, cstyle (default: python)")]
    [DefaultValue("python")]
    public string Syntax { get; set; } = "python";

    [CommandOption("-v|--verbose")]
    [Description("Enable detailed logging")]
    public bool Verbose { get; set; }

    [CommandOption("--asm-only")]
    [Description("Stop after generating assembly (do not assemble)")]
    public bool AsmOnly { get; set; }
}