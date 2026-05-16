using Snek.Lexing;
using Snek.Pipeline;

namespace Snek.Parsing;

public static class ParserExtensions
{
    public static List<T> ParseCommaSeparated<T>(
        this IEnumerator<Token> tokens,
        TokenType terminator,
        Func<Token, CompilationContext, T> parseItem,
        CompilationContext context)
    {
        List<T> items = [];

        if (tokens.Current?.Type == terminator)
        {
            return items;
        }

        do
        {
            if (tokens.Current is null)
            {
                break;
            }

            items.Add(parseItem(tokens.Current, context));
            tokens.MoveNext();
        } while (tokens.Current?.Type == TokenType.Comma);

        return items;
    }

    public static void SkipToSyncPoint(
        this IEnumerator<Token> tokens,
        params TokenType[] syncPoints)
    {
        while (tokens.Current?.Type is not (TokenType.Eof or TokenType.Newline or TokenType.Dedent)
               && !syncPoints.Contains(tokens.Current.Type))
        {
            tokens.MoveNext();
        }
    }

    public static Token? Peek(this IEnumerator<Token> tokens, int offset = 0)
    {
        _ = tokens.Current;

        for (int i = 0; i <= offset && tokens.MoveNext(); i++)
        {
            if (i != offset)
            {
                continue;
            }

            return tokens.Current;
        }

        return null;
    }
}