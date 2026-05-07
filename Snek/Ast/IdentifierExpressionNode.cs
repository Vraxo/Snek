using Snek.Lexer;

namespace Snek.Ast;

public record IdentifierExpressionNode(Token Name) : ExpressionNode;
