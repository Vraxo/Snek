using Snek.Lexing;

namespace Snek.Analysis;

public static class TypeKindExtensions
{
    public static TypeKind FromTokenType(TokenType tokenType)
    {
        return tokenType switch
        {
            TokenType.StringLiteral => TypeKind.Str,
            TokenType.CharLiteral => TypeKind.Char,
            TokenType.IntegerLiteral => TypeKind.I32,
            TokenType.FloatLiteral => TypeKind.F64,
            TokenType.KeywordTrue or TokenType.KeywordFalse => TypeKind.Bool,
            TokenType.KeywordNone => TypeKind.NoneType,
            _ => TypeKind.Unknown
        };
    }

    public static string ToTypeString(this TypeKind kind)
    {
        return kind switch
        {
            TypeKind.I32 => "i32",
            TypeKind.F64 => "f64",
            TypeKind.Bool => "bool",
            TypeKind.Str => "str",
            TypeKind.Char => "char",
            TypeKind.NoneType => "NoneType",
            TypeKind.Any => "Any",
            _ => "Unknown"
        };
    }

    public static TypeKind FromString(string typeName)
    {
        return typeName switch
        {
            "i32" => TypeKind.I32,
            "f64" => TypeKind.F64,
            "bool" => TypeKind.Bool,
            "str" => TypeKind.Str,
            "char" => TypeKind.Char,
            "NoneType" => TypeKind.NoneType,
            "Any" => TypeKind.Any,
            _ => TypeKind.Unknown
        };
    }
}