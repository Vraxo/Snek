using Snek.Lexing;

namespace Snek.Ast;

public record UnaryExpressionNode(Token Operator, ExpressionNode Operand) : ExpressionNode;
