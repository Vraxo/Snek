using Snek.Ast;
using Snek.Pipeline;

namespace Snek.Analysis;

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

    public string? AnalyzeExpression(ExpressionNode expr)
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

    public string? ResolveType(ExpressionNode expr)
    {
        return _typeResolver.Resolve(expr);
    }
}