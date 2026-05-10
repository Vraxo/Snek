using Snek.Ast;
using Snek.Diagnoistics;
using Snek.Lexer;
using Snek.Pipeline;

namespace Snek.Analysis;

public class SemanticAnalyzer : ISemanticAnalyzer
{
    private readonly Dictionary<string, SymbolInfo> _globals = [];
    private readonly Stack<Scope> _scopes = new();
    private CompilationContext _context = null!;

    public void Analyze(AstNode root, CompilationContext context)
    {
        _context = context;

        _globals.Clear();
        _scopes.Clear();
        _scopes.Push(new(null)); // Global scope

        if (root is not ProgramNode program)
        {
            return;
        }

        // First pass: collect global declarations
        foreach (StatementNode stmt in program.Statements)
        {
            if (stmt is not FunctionDefNode func)
            {
                continue;
            }

            new FunctionType(
                func.Name.Value,
                func.Parameters,
                func.ReturnType?.Name.Value ?? "void");
            _globals[func.Name.Value] = new("function", func.Name.Line, func.Name.Column);
        }

        // Second pass: analyze function bodies
        foreach (StatementNode stmt in program.Statements)
        {
            AnalyzeStatement(stmt, null);
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
                    AnalyzeExpression(expr.Expression);
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
            _scopes.Pop();
        }
    }

    private void AnalyzeFunction(FunctionDefNode func)
    {
        // Register parameters in local scope
        foreach (ParameterNode param in func.Parameters)
        {
            string paramType = param.TypeAnnotation?.Name.Value ?? "Any";

            _scopes.Peek().Symbols[param.Name.Value] = new(
                paramType,
                param.Name.Line,
                param.Name.Column);
        }

        // Analyze body
        string returnType = func.ReturnType?.Name.Value ?? "void";
        foreach (StatementNode bodyStmt in func.Body)
        {
            AnalyzeStatement(bodyStmt, returnType);
        }
    }

    private void AnalyzeIf(IfStatementNode ifs)
    {
        string? condType = AnalyzeExpression(ifs.Condition);

        if (condType is not "bool" and not null)
        {
            int conditionLine = ifs.Condition is IdentifierExpressionNode idExpr ? idExpr.Name.Line : -1;

            _context.Diagnostics.Add(new Diagnostic(
                _context.SourceName,
                $"Condition must be bool, got '{condType}'",
                conditionLine,
                -1,
                DiagnosticSeverity.Error));
        }

        foreach (StatementNode stmt in ifs.ThenBody)
        {
            AnalyzeStatement(stmt, null);
        }

        if (ifs.ElseBody == null)
        {
            return;
        }

        foreach (StatementNode stmt in ifs.ElseBody)
        {
            AnalyzeStatement(stmt, null);
        }
    }

    private void AnalyzeWhile(WhileStatementNode whl)
    {
        string? condType = AnalyzeExpression(whl.Condition);

        if (condType is not "bool" and not null)
        {
            _context.Diagnostics.Add(new(
                _context.SourceName,
                $"While condition must be bool, got '{condType}'",
                -1,
                -1,
                DiagnosticSeverity.Error));
        }

        foreach (StatementNode stmt in whl.Body)
        {
            AnalyzeStatement(stmt, null);
        }
    }

    private void AnalyzeReturn(ReturnStatementNode ret, string? expectedReturnType)
    {
        if (ret.Value == null)
        {
            if (expectedReturnType is not null and not "void" and not "Any")
            {
                _context.Diagnostics.Add(new Diagnostic(
                    _context.SourceName,
                    $"Non-void function must return a value",
                    -1,
                    -1,
                    DiagnosticSeverity.Error));
            }

            return;
        }

        string? actualType = AnalyzeExpression(ret.Value);

        if (expectedReturnType == null
            || actualType == null
            || actualType == expectedReturnType
            || expectedReturnType == "Any")
        {
            return;
        }

        _context.Diagnostics.Add(new(
            _context.SourceName,
            $"Return type mismatch: expected '{expectedReturnType}', got '{actualType}'",
            -1,
            -1,
            DiagnosticSeverity.Error));
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
        foreach (Scope scope in _scopes)
        {
            if (scope.Symbols.TryGetValue(id.Name.Value, out SymbolInfo? info))
            {
                info.IsRead = true;
                return info.Type;
            }
        }
        // Check globals
        if (_globals.TryGetValue(id.Name.Value, out SymbolInfo? global))
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
        if (_globals.TryGetValue(calleeName, out SymbolInfo? funcInfo)
            && funcInfo.Type == "function"
            && funcInfo.Metadata is FunctionType ft)
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

        // Built-in: print returns NoneType
        if (calleeName == "print")
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

        // Arithmetic ops: int/float promotion
        if (bin.Operator.Type
            is TokenType.Plus
            or TokenType.Minus
            or TokenType.Star
            or TokenType.Slash)
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
        return bin.Operator.Type == TokenType.Plus
            && left == "string"
            && right == "string" ? "string" : "Any";
    }

    private SymbolInfo? LookupSymbol(string name)
    {
        foreach (Scope scope in _scopes)
        {
            if (scope.Symbols.TryGetValue(name, out SymbolInfo? info))
            {
                return info;
            }
        }

        return _globals.TryGetValue(name, out SymbolInfo? global)
            ? global
            : null;
    }
}