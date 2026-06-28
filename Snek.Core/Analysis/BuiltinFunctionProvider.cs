namespace Snek.Core.Analysis;

public static class BuiltinFunctionProvider
{
    private static readonly Dictionary<string, TypeKind> _builtinReturnTypes = new()
    {
        ["print"] = TypeKind.NoneType,
        ["pause"] = TypeKind.NoneType,
        ["read_i32"] = TypeKind.I32
    };

    public static bool IsBuiltin(string name)
    {
        return _builtinReturnTypes.ContainsKey(name);
    }

    public static TypeKind GetReturnType(string name)
    {
        return _builtinReturnTypes.GetValueOrDefault(name, TypeKind.Unknown);
    }
}