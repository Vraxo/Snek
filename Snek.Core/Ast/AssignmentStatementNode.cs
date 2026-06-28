using Snek.Core.Lexing;

namespace Snek.Core.Ast;

public record AssignmentStatementNode(Token Name, ExpressionNode Value) : StatementNode;