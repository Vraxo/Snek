using Snek.Lexing;

namespace Snek.Ast;

public record LiteralExpressionNode(Token Value) : ExpressionNode;
