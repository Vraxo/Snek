using Snek.Ast;
using Snek.Diagnoistics;
using Snek.Lexer;
using Snek.Pipeline;

namespace Snek.Analysis;

public class StatementAnalyzer
{
    private readonly ScopeManager _scopeManager;
    private readonly ExpressionAnalyzer _expressionAnalyzer;
    private CompilationContext _context = null!;

    public StatementAnalyzer(ScopeManager scopeManager, ExpressionAnalyzer expressionAnalyzer)
    {
        _scopeManager = scopeManager;
        _expressionAnalyzer = expressionAnalyzer;
    }

    public void Initialize(CompilationContext context)
    {
        _context = context;
        _expressionAnalyzer.Initialize(context);
    }

    public void AnalyzeStatement(StatementNode stmt, TypeKind? expectedReturnType)
    {
        switch (stmt)
        {
            case FunctionDefNode func:
                AnalyzeFunction(func);
                break;
            case ExpressionStatementNode expr:
                _expressionAnalyzer.AnalyzeExpression(expr.Expression);
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
            case VariableDeclarationNode varDecl:
                AnalyzeVariableDeclaration(varDecl);
                break;
        }
    }

    private void AnalyzeFunction(FunctionDefNode func)
    {
        _scopeManager.PushScope();
        try
        {
            foreach (ParameterNode param in func.Parameters)
            {
                TypeKind paramType = param.TypeAnnotation != null
                    ? TypeKindExtensions.FromString(param.TypeAnnotation.Name.Value)
                    : TypeKind.Any;
                _scopeManager.AddSymbol(param.Name.Value, new SymbolInfo(paramType, param.Name.Line, param.Name.Column));
            }

            TypeKind? returnType = func.ReturnType != null
                ? TypeKindExtensions.FromString(func.ReturnType.Name.Value)
                : null;
            bool hasReturn = false;

            foreach (StatementNode bodyStmt in func.Body)
            {
                AnalyzeStatement(bodyStmt, returnType);
                if (bodyStmt is ReturnStatementNode)
                    hasReturn = true;
            }

            if (returnType.HasValue && returnType != TypeKind.NoneType && !hasReturn && returnType != TypeKind.Any)
            {
                _context.Diagnostics.Add(new Diagnostic(
                    _context.SourceName,
                    $"Non-void function '{func.Name.Value}' must return a value",
                    func.Name.Line, func.Name.Column, DiagnosticSeverity.Error));
            }
        }
        finally
        {
            _scopeManager.PopScope();
        }
    }

    private void AnalyzeVariableDeclaration(VariableDeclarationNode varDecl)
    {
        TypeKind varType = TypeKindExtensions.FromString(varDecl.Type.Name.Value);

        if (_scopeManager.IsSymbolDefinedInCurrentScope(varDecl.Name.Value))
        {
            _context.Diagnostics.Add(new Diagnostic(
                _context.SourceName,
                $"Variable '{varDecl.Name.Value}' already declared in this scope",
                varDecl.Name.Line, varDecl.Name.Column, DiagnosticSeverity.Error));
            return;
        }

        if (varDecl.Initializer != null)
        {
            TypeKind? initType = _expressionAnalyzer.AnalyzeExpression(varDecl.Initializer);
            if (initType != null && initType != varType && varType != TypeKind.Any)
            {
                _context.Diagnostics.Add(new Diagnostic(
                    _context.SourceName,
                    $"Type mismatch: cannot assign '{initType.Value.ToTypeString()}' to variable of type '{varType.ToTypeString()}'",
                    varDecl.Name.Line, varDecl.Name.Column, DiagnosticSeverity.Error));
            }
        }

        var symbolInfo = new SymbolInfo(varType, varDecl.Name.Line, varDecl.Name.Column);
        _scopeManager.AddSymbol(varDecl.Name.Value, symbolInfo);
        
        if (_scopeManager.IsGlobalScope)
        {
            _scopeManager.AddGlobalSymbol(varDecl.Name.Value, symbolInfo);
        }
    }

    private void AnalyzeIf(IfStatementNode ifs)
    {
        TypeKind? condType = _expressionAnalyzer.AnalyzeExpression(ifs.Condition);

        if (condType != TypeKind.Bool && condType != null)
        {
            int conditionLine = ifs.Condition is IdentifierExpressionNode idExpr ? idExpr.Name.Line : -1;
            _context.Diagnostics.Add(new Diagnostic(
                _context.SourceName,
                $"Condition must be bool, got '{condType.Value.ToTypeString()}'",
                conditionLine,
                -1,
                DiagnosticSeverity.Error));
        }

        _scopeManager.PushScope();
        try
        {
            foreach (StatementNode stmt in ifs.ThenBody)
                AnalyzeStatement(stmt, null);
        }
        finally
        {
            _scopeManager.PopScope();
        }

        if (ifs.ElseBody == null)
            return;

        _scopeManager.PushScope();
        try
        {
            foreach (StatementNode stmt in ifs.ElseBody)
                AnalyzeStatement(stmt, null);
        }
        finally
        {
            _scopeManager.PopScope();
        }
    }

    private void AnalyzeWhile(WhileStatementNode whl)
    {
        TypeKind? condType = _expressionAnalyzer.AnalyzeExpression(whl.Condition);

        if (condType != TypeKind.Bool && condType != null)
        {
            _context.Diagnostics.Add(new Diagnostic(
                _context.SourceName,
                $"While condition must be bool, got '{condType.Value.ToTypeString()}'",
                -1,
                -1,
                DiagnosticSeverity.Error));
        }

        foreach (StatementNode stmt in whl.Body)
            AnalyzeStatement(stmt, null);
    }

    private void AnalyzeReturn(ReturnStatementNode ret, TypeKind? expectedReturnType)
    {
        if (ret.Value == null)
        {
            if (expectedReturnType.HasValue && expectedReturnType != TypeKind.NoneType && expectedReturnType != TypeKind.Any)
            {
                _context.Diagnostics.Add(new Diagnostic(
                    _context.SourceName,
                    "Non-void function must return a value",
                    -1,
                    -1,
                    DiagnosticSeverity.Error));
            }
            return;
        }

        TypeKind? actualType = _expressionAnalyzer.AnalyzeExpression(ret.Value);

        if (expectedReturnType == null || actualType == null || actualType == expectedReturnType || expectedReturnType == TypeKind.Any)
            return;

        _context.Diagnostics.Add(new Diagnostic(
            _context.SourceName,
            $"Return type mismatch: expected '{expectedReturnType.Value.ToTypeString()}', got '{actualType.Value.ToTypeString()}'",
            -1,
            -1,
            DiagnosticSeverity.Error));
    }
}