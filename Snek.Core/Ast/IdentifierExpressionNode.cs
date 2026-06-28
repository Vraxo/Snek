using Snek.Core.Lexing;

namespace Snek.Core.Ast;

public record IdentifierExpressionNode(Token Name) : ExpressionNode;
