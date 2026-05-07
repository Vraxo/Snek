namespace Snek.Ast;

public record WhileStatementNode(
    ExpressionNode Condition,
    List<StatementNode> Body,
    int IndentLevel) : StatementNode;
