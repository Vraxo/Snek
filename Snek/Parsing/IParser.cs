using Snek.Ast;
using Snek.Lexing;
using Snek.Pipeline;

namespace Snek.Parsing;

public interface IParser
{
    AstNode Parse(IEnumerable<Token> tokens, CompilationContext context);
}