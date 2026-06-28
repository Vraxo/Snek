using Snek.Core.Pipeline;

namespace Snek.Core.Lexing;

public interface ILexer
{
    IEnumerable<Token> Tokenize(string source, CompilationContext context);
}