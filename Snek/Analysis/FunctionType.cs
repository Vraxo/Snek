using Snek.Ast;

namespace Snek.Analysis;

public record FunctionType(string Name, List<ParameterNode> Parameters, string ReturnType)
{
    public override string ToString()
    {
        return $"fn({string.Join(", ", Parameters)}) -> {ReturnType}";
    }
}