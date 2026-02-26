using Snek.Ast;
using Snek.Pipeline;

namespace Snek.Analysis;

/// <summary>
/// Abstract semantic analyzer contract. Validates AST semantics and builds symbol tables.
/// Swappable implementation enables different type systems or analysis strategies.
/// </summary>
public interface ISemanticAnalyzer
{
    /// <summary>
    /// Performs semantic analysis on the AST, populating context with diagnostics and symbol info.
    /// </summary>
    void Analyze(AstNode root, CompilationContext context);

    /// <summary>
    /// Resolves the type of an expression node within the given context.
    /// Returns the fully qualified type name or null if unresolvable.
    /// </summary>
    string? ResolveType(ExpressionNode expr, CompilationContext context);
}