using Snek.Ast;
using Snek.Pipeline;

namespace Snek.Generation;

/// <summary>
/// Abstract code generator contract. Converts analyzed AST to target output.
/// Swappable implementations enable multiple backends (x86, WASM, C, etc.).
/// </summary>
public interface ICodeGenerator
{
    /// <summary>
    /// Generates target code from the analyzed AST.
    /// Returns the generated output or null on failure.
    /// </summary>
    string? Generate(AstNode root, CompilationContext context);
}