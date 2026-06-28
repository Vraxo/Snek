using Snek.Core.Ast;
using Snek.Core.Pipeline;

namespace Snek.Core.Generation;

public interface ICodeGenerator
{
    string? Generate(AstNode root, CompilationContext context);
}