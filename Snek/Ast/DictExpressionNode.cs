namespace Snek.Ast;

public record DictExpressionNode(List<(ExpressionNode Key, ExpressionNode Value)> Items) : ExpressionNode;
