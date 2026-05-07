namespace Snek.Ast;

public record ProgramNode(List<StatementNode> Statements) : AstNode;