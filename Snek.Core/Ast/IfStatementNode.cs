namespace Snek.Core.Ast;

public record IfStatementNode(
    ExpressionNode Condition,
    List<StatementNode> ThenBody,
    List<StatementNode>? ElseBody) : StatementNode;