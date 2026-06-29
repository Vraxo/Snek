namespace Snek.Core.Ast;

public record AssignmentStatementNode(ExpressionNode Target, ExpressionNode Value) : StatementNode;