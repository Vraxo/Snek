using Snek.Lexer;
using Snek.Pipeline;

namespace Snek.Parser;

/// <summary>
/// Extension methods for token stream parsing patterns.
/// Keeps parser implementations clean and reusable.
/// </summary>
public static class ParserExtensions
{
    /// <summary>
    /// Parses a comma-separated list of items until the terminator token.
    /// </summary>
    public static List<T> ParseCommaSeparated<T>(
        this IEnumerator<Token> tokens,
        TokenType terminator,
        Func<Token, CompilationContext, T> parseItem,
        CompilationContext context)
    {
        var items = new List<T>();
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
            _ = tokens.MoveNext();
        } while (tokens.Current?.Type == TokenType.Comma);

        return items;
    }

    /// <summary>
    /// Skips tokens until a synchronization point (newline, dedent, or specific token).
    /// Used for error recovery.
    /// </summary>
    public static void SkipToSyncPoint(
        this IEnumerator<Token> tokens,
        params TokenType[] syncPoints)
    {
        while (tokens.Current?.Type is not (TokenType.Eof or TokenType.Newline or TokenType.Dedent)
               && !syncPoints.Contains(tokens.Current.Type))
        {
            _ = tokens.MoveNext();
        }
    }

    /// <summary>
    /// Peeks ahead N tokens without consuming them.
    /// </summary>
    public static Token? Peek(this IEnumerator<Token> tokens, int offset = 0)
    {
        _ = tokens.Current;
        for (int i = 0; i <= offset && tokens.MoveNext(); i++)
        {
            if (i == offset)
            {
                return tokens.Current;
            }
        }
        return null;
    }
}