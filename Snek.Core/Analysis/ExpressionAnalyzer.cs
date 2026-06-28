using Snek.Core.Ast;
using Snek.Core.Pipeline;

namespace Snek.Core.Analysis;

public class ExpressionAnalyzer
{
    private readonly ScopeManager _scopeManager;
    private readonly CallValidator _callValidator;
    private readonly ExpressionTypeResolver _typeResolver;
    private CompilationContext _context = null!;

    public ExpressionAnalyzer(ScopeManager scopeManager)
    {
        _scopeManager = scopeManager;
        _callValidator = new(scopeManager);
        _typeResolver = new(scopeManager, _callValidator);
    }

    public void Initialize(CompilationContext context)
    {
        _context = context;
        _callValidator.Initialize(context);
        _typeResolver.Initialize(context);
    }

    public TypeKind? AnalyzeExpression(ExpressionNode expr)
    {
        if (expr is CallExpressionNode call)
        {
            AnalyzeArgumentsForSideEffects(call);
            return _callValidator.ValidateAndGetReturnType(call);
        }

        return _typeResolver.Resolve(expr);
    }

    private void AnalyzeArgumentsForSideEffects(CallExpressionNode call)
    {
        foreach (ExpressionNode arg in call.Arguments)
        {
            AnalyzeExpression(arg);
        }
    }

    public TypeKind? ResolveType(ExpressionNode expr)
    {
        return _typeResolver.Resolve(expr);
    }
}