using Snek.Lexing;

namespace Snek.Ast;

public record IdentifierExpressionNode(Token Name) : ExpressionNode;
