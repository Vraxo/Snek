namespace Snek.Lexer;

/// <summary>
/// Represents a lexical token with type, value, and source position.
/// </summary>
public record Token(TokenType Type, string Value, int Line, int Column)
{
    public override string ToString()
    {
        return $"[{Line}:{Column}] {Type}='{Value}'";
    }
}