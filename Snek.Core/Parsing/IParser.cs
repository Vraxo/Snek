using Snek.Core.Ast;
using Snek.Core.Lexing;
using Snek.Core.Pipeline;

namespace Snek.Core.Parsing;

public interface IParser
{
    AstNode Parse(IEnumerable<Token> tokens, CompilationContext context);
}