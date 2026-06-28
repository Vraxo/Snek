using Snek.Core.Lexing;

namespace Snek.Core.Ast;

public record LiteralExpressionNode(Token Value) : ExpressionNode;