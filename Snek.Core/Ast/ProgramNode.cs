namespace Snek.Core.Ast;

public record ProgramNode(List<StatementNode> Statements) : AstNode;