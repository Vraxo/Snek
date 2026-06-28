namespace Snek.Core.Ast;

public record IndexExpressionNode(ExpressionNode Target, ExpressionNode Index) : ExpressionNode;
