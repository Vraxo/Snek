using Snek.Lexer;

namespace Snek.Analysis;

public static class BinaryOperatorTypeResolver
{
    public static string Resolve(string? leftType, string? rightType, TokenType operatorType)
    {
        if (leftType == null || rightType == null)
            return "Any";

        if (IsArithmeticOperator(operatorType))
            return ResolveArithmeticPromotion(leftType, rightType);

        if (IsComparisonOperator(operatorType))
            return "bool";

        if (IsStringConcatenation(operatorType, leftType, rightType))
            return "str";

        return "Any";
    }

    private static string ResolveArithmeticPromotion(string leftType, string rightType)
    {
        if (leftType == "f64" || rightType == "f64")
            return "f64";
        if (leftType == "i32" && rightType == "i32")
            return "i32";
        return "Any";
    }

    private static bool IsStringConcatenation(TokenType operatorType, string leftType, string rightType)
    {
        return operatorType == TokenType.Plus && leftType == "str" && rightType == "str";
    }

    private static bool IsArithmeticOperator(TokenType type)
    {
        return type is TokenType.Plus or TokenType.Minus or TokenType.Star or TokenType.Slash;
    }

    private static bool IsComparisonOperator(TokenType type)
    {
        return type is TokenType.DoubleEquals
            or TokenType.NotEquals
            or TokenType.LessThan
            or TokenType.GreaterThan
            or TokenType.LessEqual
            or TokenType.GreaterEqual;
    }
}