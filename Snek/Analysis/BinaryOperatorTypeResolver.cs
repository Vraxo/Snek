using Snek.Lexing;

namespace Snek.Analysis;

public static class BinaryOperatorTypeResolver
{
    public static TypeKind Resolve(TypeKind? leftType, TypeKind? rightType, TokenType operatorType)
    {
        if (leftType == null || rightType == null)
        {
            return TypeKind.Any;
        }

        if (IsArithmeticOperator(operatorType))
        {
            return ResolveArithmeticPromotion(leftType.Value, rightType.Value);
        }

        if (IsComparisonOperator(operatorType))
        {
            return TypeKind.Bool;
        }

        if (IsStringConcatenation(operatorType, leftType.Value, rightType.Value))
        {
            return TypeKind.Str;
        }

        return TypeKind.Any;
    }

    private static TypeKind ResolveArithmeticPromotion(TypeKind leftType, TypeKind rightType)
    {
        if (leftType == TypeKind.F64 || rightType == TypeKind.F64)
        {
            return TypeKind.F64;
        }

        if (leftType == TypeKind.I32 && rightType == TypeKind.I32)
        {
            return TypeKind.I32;
        }

        return TypeKind.Any;
    }

    private static bool IsStringConcatenation(TokenType operatorType, TypeKind leftType, TypeKind rightType)
    {
        return operatorType == TokenType.Plus && leftType == TypeKind.Str && rightType == TypeKind.Str;
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