using Snek.Core.Lexing;

namespace Snek.Core.Ast;

public record TypeNode(Token Name, List<TypeNode>? GenericArgs) : AstNode
{
    public static TypeNode Simple(Token name)
    {
        return new(name, null);
    }

    public static TypeNode Generic(Token name, List<TypeNode> args)
    {
        return new(name, args);
    }
}