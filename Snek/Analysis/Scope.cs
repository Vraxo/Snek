namespace Snek.Analysis;

public class Scope
{
    public Scope? Parent { get; }
    public Dictionary<string, SymbolInfo> Symbols { get; } = [];

    public Scope(Scope? parent)
    {
        Parent = parent;
    }
}