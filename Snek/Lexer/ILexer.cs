using Snek.Pipeline;

namespace Snek.Lexer;

/// <summary>
/// Abstract lexer contract. Implementations define syntax-specific tokenization rules.
/// Swapping implementations changes the language syntax without affecting downstream stages.
/// </summary>
public interface ILexer
{
    /// <summary>
    /// Converts source text into a stream of tokens.
    /// Reports lexical errors via the context.
    /// </summary>
    IEnumerable<Token> Tokenize(string source, CompilationContext context);
}