using Snek.Pipeline;

namespace Snek.Lexing;

public interface ILexer
{
    IEnumerable<Token> Tokenize(string source, CompilationContext context);
}