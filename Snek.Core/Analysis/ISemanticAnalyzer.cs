using Snek.Core.Ast;
using Snek.Core.Pipeline;

namespace Snek.Core.Analysis;

public interface ISemanticAnalyzer
{
    void Analyze(AstNode root, CompilationContext context);

    TypeKind? ResolveType(ExpressionNode expr, CompilationContext context);
}