using Snek.Ast;
using Snek.Diagnoistics;
using Snek.Lexer;
using Snek.Pipeline;

namespace Snek.Analysis;

public class ExpressionAnalyzer
{
    private readonly ScopeManager _scopeManager;
    private CompilationContext _context = null!;

    public ExpressionAnalyzer(ScopeManager scopeManager)
    {
        _scopeManager = scopeManager;
    }

    public void Initialize(CompilationContext context)
    {
        _context = context;
    }

    public string? AnalyzeExpression(ExpressionNode expr)
    {
        return expr switch
        {
            LiteralExpressionNode lit => AnalyzeLiteral(lit),
            IdentifierExpressionNode id => ResolveIdentifier(id),
            CallExpressionNode call => AnalyzeCall(call),
            BinaryExpressionNode bin => ResolveBinaryType(bin),
            UnaryExpressionNode unary => AnalyzeExpression(unary.Operand),
            _ => "Any"
        };
    }

    public string? ResolveType(ExpressionNode expr)
    {
        return expr switch
        {
            LiteralExpressionNode lit => lit.Value.Type switch
            {
                TokenType.StringLiteral => "str",
                TokenType.CharLiteral => "char",
                TokenType.IntegerLiteral => "i32",
                TokenType.FloatLiteral => "f64",
                TokenType.KeywordTrue or TokenType.KeywordFalse => "bool",
                TokenType.KeywordNone => "NoneType",
                _ => null
            },
            IdentifierExpressionNode id => _scopeManager.LookupSymbol(id.Name.Value)?.Type,
            CallExpressionNode call => ResolveCallType(call),
            BinaryExpressionNode bin => ResolveBinaryType(bin),
            _ => null
        };
    }

    private static string? AnalyzeLiteral(LiteralExpressionNode lit)
    {
        return lit.Value.Type switch
        {
            TokenType.StringLiteral => "str",
            TokenType.CharLiteral => "char",
            TokenType.IntegerLiteral => "i32",
            TokenType.FloatLiteral => "f64",
            TokenType.KeywordTrue or TokenType.KeywordFalse => "bool",
            TokenType.KeywordNone => "NoneType",
            _ => "Any"
        };
    }

    private string? ResolveIdentifier(IdentifierExpressionNode id)
    {
        SymbolInfo? symbol = _scopeManager.LookupSymbol(id.Name.Value);
        if (symbol != null)
        {
            return symbol.Type;
        }

        _context.Diagnostics.Add(new(
            _context.SourceName,
            $"Undefined identifier '{id.Name.Value}'",
            id.Name.Line,
            id.Name.Column,
            DiagnosticSeverity.Error));

        return null;
    }

    private string? AnalyzeCall(CallExpressionNode call)
    {
        foreach (ExpressionNode arg in call.Arguments)
        {
            AnalyzeExpression(arg);
        }

        return ResolveCallType(call);
    }

    private string? ResolveCallType(CallExpressionNode call)
    {
        string? calleeName = null;

        if (call.Callee is IdentifierExpressionNode callId)
        {
            calleeName = callId.Name.Value;
        }

        if (calleeName == null)
        {
            return "Any";
        }

        // Check if it's a known function
        SymbolInfo? funcInfo = _scopeManager.LookupFunction(calleeName);
        if (funcInfo?.Metadata is FunctionType ft)
        {
            // Basic arity check
            if (call.Arguments.Count != ft.Parameters.Count)
            {
                int callLine = call.Callee is IdentifierExpressionNode cid ? cid.Name.Line : -1;
                _context.Diagnostics.Add(new Diagnostic(
                    _context.SourceName,
                    $"Function '{calleeName}' expects {ft.Parameters.Count} args, got {call.Arguments.Count}",
                    callLine, -1,
                    DiagnosticSeverity.Error));
            }

            return ft.ReturnType;
        }

        // Built-ins: print returns NoneType, pause returns NoneType
        if (calleeName is "print" or "pause")
        {
            return "NoneType";
        }

        _context.Diagnostics.Add(new(
            _context.SourceName,
            $"Undefined function '{calleeName}'",
            call.Callee is IdentifierExpressionNode callIdent ? callIdent.Name.Line : -1, -1,
            DiagnosticSeverity.Error));

        return null;
    }

    private string? ResolveBinaryType(BinaryExpressionNode bin)
    {
        string? left = AnalyzeExpression(bin.Left);
        string? right = AnalyzeExpression(bin.Right);

        // Arithmetic ops: i32/f64 promotion
        if (bin.Operator.Type
            is TokenType.Plus
            or TokenType.Minus
            or TokenType.Star
            or TokenType.Slash)
        {
            if (left == "f64" || right == "f64")
            {
                return "f64";
            }

            if (left == "i32" && right == "i32")
            {
                return "i32";
            }
        }

        // Comparison ops: always bool
        if (bin.Operator.Type
            is TokenType.DoubleEquals
            or TokenType.NotEquals
            or TokenType.LessThan
            or TokenType.GreaterThan
            or TokenType.LessEqual
            or TokenType.GreaterEqual)
        {
            return "bool";
        }

        // String concat
        if (bin.Operator.Type == TokenType.Plus
            && left == "str"
            && right == "str")
        {

            // String concat
            return "str";
        }

        return "Any";
    }
}