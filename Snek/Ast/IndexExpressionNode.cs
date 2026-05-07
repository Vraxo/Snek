namespace Snek.Ast;

public record IndexExpressionNode(ExpressionNode Target, ExpressionNode Index) : ExpressionNode;
