using Snek.Lexer;

namespace Snek.Ast;

public record MemberAccessExpressionNode(ExpressionNode Object, Token Member) : ExpressionNode;
