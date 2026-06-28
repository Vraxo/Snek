namespace Snek.Core.Ast;

public record ReturnStatementNode(ExpressionNode? Value) : StatementNode;
