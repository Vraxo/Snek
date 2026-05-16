using Snek.Lexing;

namespace Snek.Ast;

public record BinaryExpressionNode(ExpressionNode Left, Token Operator, ExpressionNode Right) : ExpressionNode;
