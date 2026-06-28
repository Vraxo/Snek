using Snek.Core.Ast;
using Snek.Core.Diagnoistics;
using Snek.Core.Pipeline;

namespace Snek.Core.Analysis;

public class CallValidator
{
    private readonly ScopeManager _scopeManager;
    private CompilationContext _context = null!;

    public CallValidator(ScopeManager scopeManager)
    {
        _scopeManager = scopeManager;
    }

    public void Initialize(CompilationContext context)
    {
        _context = context;
    }

    public TypeKind? ValidateAndGetReturnType(CallExpressionNode call)
    {
        string? calleeName = ExtractCalleeName(call);
        if (calleeName == null)
        {
            return TypeKind.Any;
        }

        if (BuiltinFunctionProvider.IsBuiltin(calleeName))
        {
            return BuiltinFunctionProvider.GetReturnType(calleeName);
        }

        if (TryGetUserDefinedFunction(calleeName, out FunctionType? functionType))
        {
            ValidateArgumentCount(call, calleeName, functionType);
            return functionType.ReturnType;
        }

        ReportUndefinedFunction(call, calleeName);
        return null;
    }

    private string? ExtractCalleeName(CallExpressionNode call)
    {
        return call.Callee is IdentifierExpressionNode identifier
            ? identifier.Name.Value
            : null;
    }

    private bool TryGetUserDefinedFunction(string calleeName, out FunctionType? functionType)
    {
        SymbolInfo? funcInfo = _scopeManager.LookupFunction(calleeName);
        if (funcInfo?.Metadata is FunctionType ft)
        {
            functionType = ft;
            return true;
        }
        functionType = null;
        return false;
    }

    private void ValidateArgumentCount(CallExpressionNode call, string calleeName, FunctionType functionType)
    {
        if (call.Arguments.Count == functionType.Parameters.Count)
        {
            return;
        }

        int callLine = call.Callee is IdentifierExpressionNode identifier ? identifier.Name.Line : -1;
        _context.Diagnostics.Add(new(
            _context.SourceName,
            $"Function '{calleeName}' expects {functionType.Parameters.Count} args, got {call.Arguments.Count}",
            callLine, -1,
            DiagnosticSeverity.Error));
    }

    private void ReportUndefinedFunction(CallExpressionNode call, string calleeName)
    {
        int line = call.Callee is IdentifierExpressionNode identifier ? identifier.Name.Line : -1;
        _context.Diagnostics.Add(new(
            _context.SourceName,
            $"Undefined function '{calleeName}'",
            line, -1,
            DiagnosticSeverity.Error));
    }
}