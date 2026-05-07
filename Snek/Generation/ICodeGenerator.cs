using Snek.Ast;
using Snek.Pipeline;

namespace Snek.Generation;

public interface ICodeGenerator
{
    string? Generate(AstNode root, CompilationContext context);
}