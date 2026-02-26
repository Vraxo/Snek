using Snek.Ast;
using Snek.Lexer;
using Snek.Pipeline;

namespace Snek.Parser;

/// <summary>
/// Abstract parser contract. Implementations convert tokens to AST based on syntax rules.
/// Swapping implementations changes language grammar without affecting lexer or analyzer.
/// </summary>
public interface IParser
{
    /// <summary>
    /// Parses a token stream into an AST. Reports errors via context.
    /// </summary>
    AstNode Parse(IEnumerable<Token> tokens, CompilationContext context);
}