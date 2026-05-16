using Snek.Lexing;

namespace Snek.Ast;

public record MemberAccessExpressionNode(ExpressionNode Object, Token Member) : ExpressionNode;
