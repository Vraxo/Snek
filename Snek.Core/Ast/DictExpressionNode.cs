namespace Snek.Core.Ast;

public record DictExpressionNode(List<(ExpressionNode Key, ExpressionNode Value)> Items) : ExpressionNode;
