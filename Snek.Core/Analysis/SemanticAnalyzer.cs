using Snek.Core.Ast;
using Snek.Core.Pipeline;

namespace Snek.Core.Analysis;

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

    public TypeKind? ResolveType(ExpressionNode expr, CompilationContext context)
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
            if (statement is FunctionDefNode func)
            {
                RegisterGlobalFunction(func);
            }
            else if (statement is ExternFunctionDefNode extFunc)
            {
                RegisterExternFunction(extFunc);
            }
        }
    }

    private void RegisterGlobalFunction(FunctionDefNode func)
    {
        TypeKind? returnType = func.ReturnType != null
            ? TypeKindExtensions.FromString(func.ReturnType.Name.Value)
            : null;

        FunctionType funcType = new(
            func.Name.Value,
            func.Parameters,
            returnType);

        _scopeManager.AddGlobalSymbol(
            func.Name.Value,
            new(TypeKind.Function, func.Name.Line, func.Name.Column, funcType));
    }

    private void RegisterExternFunction(ExternFunctionDefNode extFunc)
    {
        TypeKind? returnType = extFunc.ReturnType != null
            ? TypeKindExtensions.FromString(extFunc.ReturnType.Name.Value)
            : null;

        FunctionType funcType = new(
            extFunc.Name.Value,
            extFunc.Parameters,
            returnType);

        _scopeManager.AddGlobalSymbol(
            extFunc.Name.Value,
            new(TypeKind.Function, extFunc.Name.Line, extFunc.Name.Column, funcType));
    }

    private void AnalyzeAllStatements(ProgramNode program)
    {
        foreach (StatementNode statement in program.Statements)
        {
            _statementAnalyzer.AnalyzeStatement(statement, null);
        }
    }
}