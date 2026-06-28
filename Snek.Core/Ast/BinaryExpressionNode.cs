using Snek.Core.Lexing;

namespace Snek.Core.Ast;

public record BinaryExpressionNode(ExpressionNode Left, Token Operator, ExpressionNode Right) : ExpressionNode;
