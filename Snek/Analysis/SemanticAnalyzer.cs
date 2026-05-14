using Snek.Ast;
using Snek.Pipeline;

namespace Snek.Analysis;

public class SemanticAnalyzer : ISemanticAnalyzer
{
    private readonly ScopeManager _scopeManager = new();
    private readonly ExpressionAnalyzer _expressionAnalyzer;
    private readonly StatementAnalyzer _statementAnalyzer;
    private CompilationContext _context = null!;

    public SemanticAnalyzer()
    {
        _expressionAnalyzer = new ExpressionAnalyzer(_scopeManager);
        _statementAnalyzer = new StatementAnalyzer(_scopeManager, _expressionAnalyzer);
    }

    public void Analyze(AstNode root, CompilationContext context)
    {
        Initialize(context);

        if (root is not ProgramNode program)
        {
            return;
        }

        CollectGlobalDeclarations(program);
        AnalyzeAllStatements(program);
    }

    public string? ResolveType(ExpressionNode expr, CompilationContext context)
    {
        _context = context;
        _expressionAnalyzer.Initialize(context);
        return _expressionAnalyzer.ResolveType(expr);
    }

    private void Initialize(CompilationContext context)
    {
        _context = context;
        _scopeManager.Initialize(context);
        _statementAnalyzer.Initialize(context);
    }

    private void CollectGlobalDeclarations(ProgramNode program)
    {
        foreach (StatementNode statement in program.Statements)
        {
            if (statement is not FunctionDefNode func)
            {
                continue;
            }

            RegisterGlobalFunction(func);
        }
    }

    private void RegisterGlobalFunction(FunctionDefNode func)
    {
        FunctionType funcType = new(
            func.Name.Value,
            func.Parameters,
            func.ReturnType?.Name.Value);

        _scopeManager.AddGlobalSymbol(
            func.Name.Value,
            new("function", func.Name.Line, func.Name.Column, funcType));
    }

    private void AnalyzeAllStatements(ProgramNode program)
    {
        foreach (StatementNode statement in program.Statements)
        {
            _statementAnalyzer.AnalyzeStatement(statement, null);
        }
    }
}