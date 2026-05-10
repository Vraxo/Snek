using Snek.Pipeline;

namespace Snek.Lexer;

public interface ILexer
{
    IEnumerable<Token> Tokenize(string source, CompilationContext context);
}