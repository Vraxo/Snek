namespace Snek;

public record CompilerOptions
{
    public string? OutputPath { get; set; }
    public string? Syntax { get; set; }
    public bool Verbose { get; set; }
    public bool AsmOnly { get; set; }
}