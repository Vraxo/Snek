namespace Snek.Ast;

public record ListExpressionNode(List<ExpressionNode> Elements) : ExpressionNode;
