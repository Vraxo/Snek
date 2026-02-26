using Snek.Ast;
using Snek.Diagnoistics;
using Snek.Lexer;
using Snek.Pipeline;

namespace Snek.Analysis;

/// <summary>
/// Reference semantic analyzer for Snek's default type system.
/// Handles scope resolution, type inference, and basic type checking.
/// </summary>
public class SnekSemanticAnalyzer : ISemanticAnalyzer
{
    private readonly Dictionary<string, SymbolInfo> _globals = [];
    private readonly Stack<Scope> _scopes = new();
    private CompilationContext _context = null!;

    public void Analyze(AstNode root, CompilationContext context)
    {
        _context = context;
        _globals.Clear();
        _scopes.Clear();
        _scopes.Push(new Scope(null)); // Global scope

        if (root is ProgramNode program)
        {
            // First pass: collect global declarations
            foreach (var stmt in program.Statements)
            {
                if (stmt is FunctionDefNode func)
                {
                    _ = new FunctionType(
                        func.Name.Value,
                        func.Parameters,
                        func.ReturnType?.Name.Value ?? "void");
                    _globals[func.Name.Value] = new SymbolInfo("function", func.Name.Line, func.Name.Column);
                }
            }

            // Second pass: analyze function bodies
            foreach (var stmt in program.Statements)
            {
                AnalyzeStatement(stmt, null);
            }
        }
    }

    public string? ResolveType(ExpressionNode expr, CompilationContext context)
    {
        return expr switch
        {
            LiteralExpressionNode lit => lit.Value.Type switch
            {
                TokenType.StringLiteral => "string",
                TokenType.IntegerLiteral => "int",
                TokenType.FloatLiteral => "float",
                TokenType.KeywordTrue or TokenType.KeywordFalse => "bool",
                TokenType.KeywordNone => "NoneType",
                _ => null
            },
            IdentifierExpressionNode id => LookupSymbol(id.Name.Value)?.Type,
            CallExpressionNode call => ResolveCallType(call),
            BinaryExpressionNode bin => ResolveBinaryType(bin),
            _ => null
        };
    }

    private void AnalyzeStatement(StatementNode stmt, string? expectedReturnType)
    {
        _scopes.Push(new Scope(_scopes.Peek()));

        try
        {
            switch (stmt)
            {
                case FunctionDefNode func:
                    AnalyzeFunction(func);
                    break;
                case ExpressionStatementNode expr:
                    _ = AnalyzeExpression(expr.Expression);
                    break;
                case IfStatementNode ifs:
                    AnalyzeIf(ifs);
                    break;
                case WhileStatementNode whl:
                    AnalyzeWhile(whl);
                    break;
                case ReturnStatementNode ret:
                    AnalyzeReturn(ret, expectedReturnType);
                    break;
            }
        }
        finally
        {
            _ = _scopes.Pop();
        }
    }

    private void AnalyzeFunction(FunctionDefNode func)
    {
        // Register parameters in local scope
        foreach (var param in func.Parameters)
        {
            var paramType = param.TypeAnnotation?.Name.Value ?? "Any";
            _scopes.Peek().Symbols[param.Name.Value] = new SymbolInfo(paramType, param.Name.Line, param.Name.Column);
        }

        // Analyze body
        var returnType = func.ReturnType?.Name.Value ?? "void";
        foreach (var bodyStmt in func.Body)
        {
            AnalyzeStatement(bodyStmt, returnType);
        }
    }

    private void AnalyzeIf(IfStatementNode ifs)
    {
        var condType = AnalyzeExpression(ifs.Condition);
        if (condType is not "bool" and not null)
        {
            var conditionLine = ifs.Condition is IdentifierExpressionNode idExpr ? idExpr.Name.Line : -1;
            _context.Diagnostics.Add(new Diagnostic(
                _context.SourceName,
                $"Condition must be bool, got '{condType}'",
                conditionLine,
                -1,
                DiagnosticSeverity.Error));
        }

        foreach (var stmt in ifs.ThenBody)
        {
            AnalyzeStatement(stmt, null);
        }

        if (ifs.ElseBody != null)
        {
            foreach (var stmt in ifs.ElseBody)
            {
                AnalyzeStatement(stmt, null);
            }
        }
    }

    private void AnalyzeWhile(WhileStatementNode whl)
    {
        var condType = AnalyzeExpression(whl.Condition);
        if (condType is not "bool" and not null)
        {
            _context.Diagnostics.Add(new Diagnostic(
                _context.SourceName,
                $"While condition must be bool, got '{condType}'",
                -1, -1, DiagnosticSeverity.Error));
        }
        foreach (var stmt in whl.Body)
        {
            AnalyzeStatement(stmt, null);
        }
    }

    private void AnalyzeReturn(ReturnStatementNode ret, string? expectedReturnType)
    {
        if (ret.Value != null)
        {
            var actualType = AnalyzeExpression(ret.Value);
            if (expectedReturnType != null && actualType != null && actualType != expectedReturnType && expectedReturnType != "Any")
            {
                _context.Diagnostics.Add(new Diagnostic(
                    _context.SourceName,
                    $"Return type mismatch: expected '{expectedReturnType}', got '{actualType}'",
                    -1, -1, DiagnosticSeverity.Error));
            }
        }
        else if (expectedReturnType is not null and not "void" and not "Any")
        {
            _context.Diagnostics.Add(new Diagnostic(
                _context.SourceName,
                $"Non-void function must return a value",
                -1, -1, DiagnosticSeverity.Error));
        }
    }

    private string? AnalyzeExpression(ExpressionNode expr)
    {
        return expr switch
        {
            LiteralExpressionNode lit => lit.Value.Type switch
            {
                TokenType.StringLiteral => "string",
                TokenType.IntegerLiteral => "int",
                TokenType.FloatLiteral => "float",
                TokenType.KeywordTrue or TokenType.KeywordFalse => "bool",
                TokenType.KeywordNone => "NoneType",
                _ => "Any"
            },
            IdentifierExpressionNode id => ResolveIdentifier(id),
            CallExpressionNode call => ResolveCallType(call),
            BinaryExpressionNode bin => ResolveBinaryType(bin),
            UnaryExpressionNode unary => AnalyzeExpression(unary.Operand),
            _ => "Any"
        };
    }

    private string? ResolveIdentifier(IdentifierExpressionNode id)
    {
        // Check local scopes first
        foreach (var scope in _scopes)
        {
            if (scope.Symbols.TryGetValue(id.Name.Value, out var info))
            {
                info.IsRead = true;
                return info.Type;
            }
        }
        // Check globals
        if (_globals.TryGetValue(id.Name.Value, out var global))
        {
            return global.Type;
        }
        _context.Diagnostics.Add(new Diagnostic(
            _context.SourceName,
            $"Undefined identifier '{id.Name.Value}'",
            id.Name.Line, id.Name.Column, DiagnosticSeverity.Error));
        return null;
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
        if (_globals.TryGetValue(calleeName, out var funcInfo))
        {
            if (funcInfo.Type == "function" && funcInfo.Metadata is FunctionType ft)
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
        }

        // Built-in: print returns NoneType
        if (calleeName == "print")
        {
            return "NoneType";
        }

        _context.Diagnostics.Add(new Diagnostic(
            _context.SourceName,
            $"Undefined function '{calleeName}'",
            call.Callee is IdentifierExpressionNode callIdent ? callIdent.Name.Line : -1, -1,
            DiagnosticSeverity.Error));
        return null;
    }

    private string? ResolveBinaryType(BinaryExpressionNode bin)
    {
        var left = AnalyzeExpression(bin.Left);
        var right = AnalyzeExpression(bin.Right);

        // Arithmetic ops: int/float promotion
        if (bin.Operator.Type is TokenType.Plus or TokenType.Minus or TokenType.Star or TokenType.Slash)
        {
            if (left == "float" || right == "float")
            {
                return "float";
            }

            if (left == "int" && right == "int")
            {
                return "int";
            }
        }

        // Comparison ops: always bool
        if (bin.Operator.Type is TokenType.DoubleEquals or TokenType.NotEquals
            or TokenType.LessThan or TokenType.GreaterThan or TokenType.LessEqual or TokenType.GreaterEqual)
        {
            return "bool";
        }

        // String concat
        return bin.Operator.Type == TokenType.Plus && left == "string" && right == "string" ? "string" : "Any";
    }

    private SymbolInfo? LookupSymbol(string name)
    {
        foreach (var scope in _scopes)
        {
            if (scope.Symbols.TryGetValue(name, out var info))
            {
                return info;
            }
        }

        return _globals.TryGetValue(name, out var global) ? global : null;
    }
}

public record SymbolInfo(string Type, int Line, int Column, object? Metadata = null)
{
    public bool IsRead { get; set; } = false;
    public bool IsWritten { get; set; } = false;
}

public record FunctionType(string Name, List<ParameterNode> Parameters, string ReturnType)
{
    public override string ToString()
    {
        return $"fn({string.Join(", ", Parameters)}) -> {ReturnType}";
    }
}

public class Scope
{
    public Scope? Parent { get; }
    public Dictionary<string, SymbolInfo> Symbols { get; } = [];
    public Scope(Scope? parent)
    {
        Parent = parent;
    }
}