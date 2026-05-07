namespace Snek.Ast;

public record ReturnStatementNode(ExpressionNode? Value) : StatementNode;
