namespace Snek.Core.Ast;

public record ListExpressionNode(List<ExpressionNode> Elements) : ExpressionNode;
