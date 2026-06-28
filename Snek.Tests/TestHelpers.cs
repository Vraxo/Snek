using Snek.Core.Ast;
using Snek.Core.Lexing;

namespace Snek.Tests;

public static class TestHelpers
{
    public static Token CreateToken(TokenType type, string value, int line = 1, int column = 1)
    {
        return new Token(type, value, line, column);
    }

    public static LiteralExpressionNode CreateIntLiteral(int value)
    {
        return new LiteralExpressionNode(CreateToken(TokenType.IntegerLiteral, value.ToString()));
    }

    public static LiteralExpressionNode CreateStringLiteral(string value)
    {
        return new LiteralExpressionNode(CreateToken(TokenType.StringLiteral, value));
    }

    public static IdentifierExpressionNode CreateIdentifier(string name)
    {
        return new IdentifierExpressionNode(CreateToken(TokenType.Identifier, name));
    }

    public static BinaryExpressionNode CreateBinary(ExpressionNode left, TokenType op, ExpressionNode right)
    {
        return new BinaryExpressionNode(left, CreateToken(op, GetOperatorString(op)), right);
    }

    private static string GetOperatorString(TokenType op)
    {
        return op switch
        {
            TokenType.Plus => "+",
            TokenType.Minus => "-",
            TokenType.Star => "*",
            TokenType.Slash => "/",
            TokenType.DoubleEquals => "==",
            TokenType.NotEquals => "!=",
            TokenType.LessThan => "<",
            TokenType.GreaterThan => ">",
            TokenType.LessEqual => "<=",
            TokenType.GreaterEqual => ">=",
            _ => op.ToString()
        };
    }
}