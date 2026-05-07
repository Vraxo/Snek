using Snek.Lexer;

namespace Snek.Ast;

public record LiteralExpressionNode(Token Value) : ExpressionNode;
