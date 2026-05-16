namespace Snek.Analysis;

public record SymbolInfo(TypeKind Type, int Line, int Column, object? Metadata = null)
{
    public bool IsRead { get; set; } = false;
    public bool IsWritten { get; set; } = false;
}