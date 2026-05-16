namespace Snek.Analysis;

public static class BuiltinFunctionProvider
{
    private static readonly Dictionary<string, string> _builtinReturnTypes = new()
    {
        ["print"] = "NoneType",
        ["pause"] = "NoneType"
    };

    public static bool IsBuiltin(string name)
    {
        return _builtinReturnTypes.ContainsKey(name);
    }

    public static string? GetReturnType(string name)
    {
        return _builtinReturnTypes.GetValueOrDefault(name);
    }
}