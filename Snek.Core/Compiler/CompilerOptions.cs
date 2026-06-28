namespace Snek.Core.Compiler;

public class CompilerOptions
{
    public string? OutputPath { get; set; }
    public string Syntax { get; set; } = "python";
    public bool Verbose { get; set; }
    public bool AsmOnly { get; set; }
}