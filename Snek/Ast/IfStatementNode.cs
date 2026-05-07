namespace Snek.Ast;

public record IfStatementNode(
    ExpressionNode Condition,
    List<StatementNode> ThenBody,
    List<StatementNode>? ElseBody,
    int IndentLevel) : StatementNode;
