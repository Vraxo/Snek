using Snek.Ast;
using Snek.Diagnoistics;
using Snek.Lexer;
using Snek.Pipeline;

namespace Snek.Analysis;

public class ExpressionTypeResolver
{
    private readonly ScopeManager _scopeManager;
    private readonly CallValidator _callValidator;
    private CompilationContext _context = null!;

    public ExpressionTypeResolver(ScopeManager scopeManager, CallValidator callValidator)
    {
        _scopeManager = scopeManager;
        _callValidator = callValidator;
    }

    public void Initialize(CompilationContext context)
    {
        _context = context;
        _callValidator.Initialize(context);
    }

    public string? Resolve(ExpressionNode expr)
    {
        return expr switch
        {
            LiteralExpressionNode lit => GetLiteralType(lit),
            IdentifierExpressionNode id => ResolveIdentifier(id),
            CallExpressionNode call => _callValidator.ValidateAndGetReturnType(call),
            BinaryExpressionNode bin => ResolveBinary(bin),
            UnaryExpressionNode unary => Resolve(unary.Operand),
            _ => "Any"
        };
    }

    private string? GetLiteralType(LiteralExpressionNode lit)
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

    private string? ResolveBinary(BinaryExpressionNode bin)
    {
        string? left = Resolve(bin.Left);
        string? right = Resolve(bin.Right);

        return BinaryOperatorTypeResolver.Resolve(left, right, bin.Operator.Type);
    }
}