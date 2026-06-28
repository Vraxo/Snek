using Snek.Core.Lexing;

namespace Snek.Core.Ast;

public record UnaryExpressionNode(Token Operator, ExpressionNode Operand) : ExpressionNode;
