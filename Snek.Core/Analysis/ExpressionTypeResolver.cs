using Snek.Core.Ast;
using Snek.Core.Diagnoistics;
using Snek.Core.Pipeline;

namespace Snek.Core.Analysis;

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

    public TypeKind? Resolve(ExpressionNode expr)
    {
        return expr switch
        {
            LiteralExpressionNode lit => GetLiteralType(lit),
            IdentifierExpressionNode id => ResolveIdentifier(id),
            CallExpressionNode call => _callValidator.ValidateAndGetReturnType(call),
            BinaryExpressionNode bin => ResolveBinary(bin),
            UnaryExpressionNode unary => Resolve(unary.Operand),
            ListExpressionNode => TypeKind.List,
            IndexExpressionNode => TypeKind.Any,
            MemberAccessExpressionNode member => ResolveMemberAccess(member),
            _ => TypeKind.Any
        };
    }

    private static TypeKind GetLiteralType(LiteralExpressionNode lit)
    {
        return TypeKindExtensions.FromTokenType(lit.Value.Type);
    }

    private TypeKind? ResolveIdentifier(IdentifierExpressionNode id)
    {
        SymbolInfo? symbol = _scopeManager.LookupSymbol(id.Name.Value);

        if (symbol != null)
        {
            return symbol.Type;
        }

        _context.Diagnostics.Add(new Diagnostic(
            _context.SourceName,
            $"Undefined identifier '{id.Name.Value}'",
            id.Name.Line,
            id.Name.Column,
            DiagnosticSeverity.Error));

        return null;
    }

    private TypeKind ResolveBinary(BinaryExpressionNode bin)
    {
        TypeKind? left = Resolve(bin.Left);
        TypeKind? right = Resolve(bin.Right);
        return BinaryOperatorTypeResolver.Resolve(left, right, bin.Operator.Type);
    }

    private TypeKind ResolveMemberAccess(MemberAccessExpressionNode member)
    {
        if (member.Member.Value == "length")
        {
            return TypeKind.I32;
        }
        return TypeKind.Any;
    }
}