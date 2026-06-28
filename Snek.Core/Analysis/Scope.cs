namespace Snek.Core.Analysis;

public class Scope
{
    public Scope? Parent { get; init; }
    public Dictionary<string, SymbolInfo> Symbols { get; } = [];
}