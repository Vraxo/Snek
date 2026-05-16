namespace Snek.Lexing;

public record Token(TokenType Type, string Value, int Line, int Column)
{
    public override string ToString()
    {
        return $"[{Line}:{Column}] {Type}='{Value}'";
    }
}