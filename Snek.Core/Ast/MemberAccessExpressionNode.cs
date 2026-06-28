using Snek.Core.Lexing;

namespace Snek.Core.Ast;

public record MemberAccessExpressionNode(ExpressionNode Object, Token Member) : ExpressionNode;
