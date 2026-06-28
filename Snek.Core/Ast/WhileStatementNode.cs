namespace Snek.Core.Ast;

public record WhileStatementNode(
    ExpressionNode Condition,
    List<StatementNode> Body) : StatementNode;