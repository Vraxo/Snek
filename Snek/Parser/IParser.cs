using Snek.Ast;
using Snek.Lexer;
using Snek.Pipeline;

namespace Snek.Parser;

public interface IParser
{
    AstNode Parse(IEnumerable<Token> tokens, CompilationContext context);
}