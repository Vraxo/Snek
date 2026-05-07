using Snek.Lexer;

namespace Snek.Ast;

public record UnaryExpressionNode(Token Operator, ExpressionNode Operand) : ExpressionNode;
