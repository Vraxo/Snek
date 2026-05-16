using Snek.Ast;
using Snek.Pipeline;

namespace Snek.Analysis;

public interface ISemanticAnalyzer
{
    void Analyze(AstNode root, CompilationContext context);

    TypeKind? ResolveType(ExpressionNode expr, CompilationContext context);
}