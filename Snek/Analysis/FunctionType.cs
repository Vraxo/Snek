using Snek.Ast;

namespace Snek.Analysis;

public record FunctionType(string Name, List<ParameterNode> Parameters, TypeKind? ReturnType)
{
    public override string ToString()
    {
        return ReturnType is null
            ? $"fn({string.Join(", ", Parameters)})"
            : $"fn({string.Join(", ", Parameters)}) -> {ReturnType.Value.ToTypeString()}";
    }
}