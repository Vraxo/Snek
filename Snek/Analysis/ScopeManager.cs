using Snek.Diagnoistics;
using Snek.Pipeline;

namespace Snek.Analysis;

public class ScopeManager
{
    private readonly Stack<Scope> _scopes = new();
    private readonly Dictionary<string, SymbolInfo> _globals = new();
    private CompilationContext _context = null!;

    public void Initialize(CompilationContext context)
    {
        _context = context;
        _globals.Clear();
        _scopes.Clear();
        _scopes.Push(new Scope(null)); // Global scope
    }

    public void PushScope()
    {
        _scopes.Push(new Scope(_scopes.Peek()));
    }

    public void PopScope()
    {
        _scopes.Pop();
    }

    public Scope CurrentScope => _scopes.Peek();

    public void AddGlobalSymbol(string name, SymbolInfo info)
    {
        _globals[name] = info;
    }

    public void AddSymbol(string name, SymbolInfo info)
    {
        CurrentScope.Symbols[name] = info;
    }

    public bool IsSymbolDefinedInCurrentScope(string name)
    {
        return CurrentScope.Symbols.ContainsKey(name);
    }

    public SymbolInfo? LookupSymbol(string name)
    {
        foreach (Scope scope in _scopes)
        {
            if (scope.Symbols.TryGetValue(name, out SymbolInfo? info))
            {
                info.IsRead = true;
                return info;
            }
        }

        return _globals.TryGetValue(name, out SymbolInfo? global)
            ? global
            : null;
    }

    public SymbolInfo? LookupFunction(string name)
    {
        if (_globals.TryGetValue(name, out SymbolInfo? info) && info.Type == "function")
        {
            return info;
        }
        return null;
    }

    public bool IsGlobalScope => _scopes.Count == 1;
}