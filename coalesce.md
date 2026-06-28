### `Snek.Core\Class1.cs`

```csharp
namespace Snek.Core;

public class Class1
{

}
```

---

### `Snek.Core\Snek.Core.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

---

### `Snek.Core\Analysis\BinaryOperatorTypeResolver.cs`

```csharp
using Snek.Core.Lexing;

namespace Snek.Core.Analysis;

public static class BinaryOperatorTypeResolver
{
    public static TypeKind Resolve(TypeKind? leftType, TypeKind? rightType, TokenType operatorType)
    {
        if (leftType == null || rightType == null)
        {
            return TypeKind.Any;
        }

        if (IsArithmeticOperator(operatorType))
        {
            return ResolveArithmeticPromotion(leftType.Value, rightType.Value);
        }

        if (IsComparisonOperator(operatorType))
        {
            return TypeKind.Bool;
        }

        if (IsStringConcatenation(operatorType, leftType.Value, rightType.Value))
        {
            return TypeKind.Str;
        }

        return TypeKind.Any;
    }

    private static TypeKind ResolveArithmeticPromotion(TypeKind leftType, TypeKind rightType)
    {
        if (leftType == TypeKind.F64 || rightType == TypeKind.F64)
        {
            return TypeKind.F64;
        }

        if (leftType == TypeKind.I32 && rightType == TypeKind.I32)
        {
            return TypeKind.I32;
        }

        return TypeKind.Any;
    }

    private static bool IsStringConcatenation(TokenType operatorType, TypeKind leftType, TypeKind rightType)
    {
        return operatorType == TokenType.Plus && leftType == TypeKind.Str && rightType == TypeKind.Str;
    }

    private static bool IsArithmeticOperator(TokenType type)
    {
        return type is TokenType.Plus or TokenType.Minus or TokenType.Star or TokenType.Slash;
    }

    private static bool IsComparisonOperator(TokenType type)
    {
        return type
            is TokenType.DoubleEquals
            or TokenType.NotEquals
            or TokenType.LessThan
            or TokenType.GreaterThan
            or TokenType.LessEqual
            or TokenType.GreaterEqual;
    }
}
```

---

### `Snek.Core\Analysis\BuiltinFunctionProvider.cs`

```csharp
namespace Snek.Core.Analysis;

public static class BuiltinFunctionProvider
{
    private static readonly Dictionary<string, TypeKind> _builtinReturnTypes = new()
    {
        ["print"] = TypeKind.NoneType,
        ["pause"] = TypeKind.NoneType
    };

    public static bool IsBuiltin(string name)
    {
        return _builtinReturnTypes.ContainsKey(name);
    }

    public static TypeKind GetReturnType(string name)
    {
        return _builtinReturnTypes.GetValueOrDefault(name, TypeKind.Unknown);
    }
}
```

---

### `Snek.Core\Analysis\CallValidator.cs`

```csharp
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
```

---

### `Snek.Core\Analysis\ExpressionAnalyzer.cs`

```csharp
namespace Snek.Core.Analysis;

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

    public TypeKind? AnalyzeExpression(ExpressionNode expr)
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

    public TypeKind? ResolveType(ExpressionNode expr)
    {
        return _typeResolver.Resolve(expr);
    }
}
```

---

### `Snek.Core\Analysis\ExpressionTypeResolver.cs`

```csharp
using Snek.Core.Ast;
using Snek.Core.Diagnoistics;

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
}
```

---

### `Snek.Core\Analysis\FunctionType.cs`

```csharp
using Snek.Core.Ast;

namespace Snek.Core.Analysis;

public record FunctionType(string Name, List<ParameterNode> Parameters, TypeKind? ReturnType)
{
    public override string ToString()
    {
        return ReturnType is null
            ? $"fn({string.Join(", ", Parameters)})"
            : $"fn({string.Join(", ", Parameters)}) -> {ReturnType.Value.ToTypeString()}";
    }
}
```

---

### `Snek.Core\Analysis\ISemanticAnalyzer.cs`

```csharp
using Snek.Core.Ast;
using Snek.Core.Pipeline;

namespace Snek.Core.Analysis;

public interface ISemanticAnalyzer
{
    void Analyze(AstNode root, CompilationContext context);

    TypeKind? ResolveType(ExpressionNode expr, CompilationContext context);
}
```

---

### `Snek.Core\Analysis\Scope.cs`

```csharp
namespace Snek.Core.Analysis;

public class Scope
{
    public Scope? Parent { get; init; }
    public Dictionary<string, SymbolInfo> Symbols { get; } = [];
}
```

---

### `Snek.Core\Analysis\ScopeManager.cs`

```csharp
namespace Snek.Core.Analysis;

public class ScopeManager
{
    private readonly Stack<Scope> _scopes = new();
    private readonly Dictionary<string, SymbolInfo> _globals = [];
    private CompilationContext _context = null!;

    public bool IsGlobalScope => _scopes.Count == 1;
    public Scope CurrentScope => _scopes.Peek();

    public void Initialize(CompilationContext context)
    {
        _context = context;
        _globals.Clear();
        _scopes.Clear();
        _scopes.Push(new() { Parent = null }); // Global scope
    }

    public void PushScope()
    {
        _scopes.Push(new() { Parent = _scopes.Peek() });
    }

    public void PopScope()
    {
        _scopes.Pop();
    }

    public void AddGlobalSymbol(string name, SymbolInfo info)
    {
        _globals[name] = info;
    }

    public void AddSymbol(string name, SymbolInfo info)
    {
        CurrentScope.Symbols[name] = info;
    }

    public bool IsSymbolDefinedInCurrentScope(string name)
    {
        return CurrentScope.Symbols.ContainsKey(name);
    }

    public SymbolInfo? LookupSymbol(string name)
    {
        foreach (Scope scope in _scopes)
        {
            if (!scope.Symbols.TryGetValue(name, out SymbolInfo? info))
            {
                continue;
            }

            info.IsRead = true;
            return info;
        }

        return _globals.TryGetValue(name, out SymbolInfo? global)
            ? global
            : null;
    }

    public SymbolInfo? LookupFunction(string name)
    {
        if (!_globals.TryGetValue(name, out SymbolInfo? info) || info.Type != TypeKind.Function)
        {
            return null;
        }

        return info;
    }
}
```

---

### `Snek.Core\Analysis\SemanticAnalyzer.cs`

```csharp
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
            if (statement is not FunctionDefNode func)
            {
                continue;
            }

            RegisterGlobalFunction(func);
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

    private void AnalyzeAllStatements(ProgramNode program)
    {
        foreach (StatementNode statement in program.Statements)
        {
            _statementAnalyzer.AnalyzeStatement(statement, null);
        }
    }
}
```

---

### `Snek.Core\Analysis\StatementAnalyzer.cs`

```csharp
using Snek.Core.Ast;
using Snek.Core.Diagnoistics;

namespace Snek.Core.Analysis;

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
                {
                    hasReturn = true;
                }
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

        SymbolInfo symbolInfo = new(varType, varDecl.Name.Line, varDecl.Name.Column);
        _scopeManager.AddSymbol(varDecl.Name.Value, symbolInfo);

        if (_scopeManager.IsGlobalScope)
        {
            _scopeManager.AddGlobalSymbol(varDecl.Name.Value, symbolInfo);
        }
    }

    private void AnalyzeIf(IfStatementNode ifs)
    {
        TypeKind? condType = _expressionAnalyzer.AnalyzeExpression(ifs.Condition);

        if (condType is not TypeKind.Bool and not null)
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
            {
                AnalyzeStatement(stmt, null);
            }
        }
        finally
        {
            _scopeManager.PopScope();
        }

        if (ifs.ElseBody == null)
        {
            return;
        }

        _scopeManager.PushScope();
        try
        {
            foreach (StatementNode stmt in ifs.ElseBody)
            {
                AnalyzeStatement(stmt, null);
            }
        }
        finally
        {
            _scopeManager.PopScope();
        }
    }

    private void AnalyzeWhile(WhileStatementNode whl)
    {
        TypeKind? condType = _expressionAnalyzer.AnalyzeExpression(whl.Condition);

        if (condType is not TypeKind.Bool and not null)
        {
            _context.Diagnostics.Add(new Diagnostic(
                _context.SourceName,
                $"While condition must be bool, got '{condType.Value.ToTypeString()}'",
                -1,
                -1,
                DiagnosticSeverity.Error));
        }

        foreach (StatementNode stmt in whl.Body)
        {
            AnalyzeStatement(stmt, null);
        }
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
        {
            return;
        }

        _context.Diagnostics.Add(new Diagnostic(
            _context.SourceName,
            $"Return type mismatch: expected '{expectedReturnType.Value.ToTypeString()}', got '{actualType.Value.ToTypeString()}'",
            -1,
            -1,
            DiagnosticSeverity.Error));
    }
}
```

---

### `Snek.Core\Analysis\SymbolInfo.cs`

```csharp
namespace Snek.Core.Analysis;

public record SymbolInfo(TypeKind Type, int Line, int Column, object? Metadata = null)
{
    public bool IsRead { get; set; } = false;
    public bool IsWritten { get; set; } = false;
}
```

---

### `Snek.Core\Analysis\TypeKind.cs`

```csharp
namespace Snek.Core.Analysis;

public enum TypeKind
{
    Unknown,    // Type could not be determined
    Any,        // Dynamic or unknown type (like "Any" in the old system)
    I32,        // 32-bit integer
    F64,        // 64-bit float
    Bool,       // boolean
    Str,        // string
    Char,       // character
    NoneType,   // None value
    Function,   // Represents a function (metadata contains FunctionType)
    // TODO: add List, Dict, etc. as needed
}
```

---

### `Snek.Core\Analysis\TypeKindExtensions.cs`

```csharp
namespace Snek.Core.Analysis;

public static class TypeKindExtensions
{
    public static TypeKind FromTokenType(TokenType tokenType)
    {
        return tokenType switch
        {
            TokenType.StringLiteral => TypeKind.Str,
            TokenType.CharLiteral => TypeKind.Char,
            TokenType.IntegerLiteral => TypeKind.I32,
            TokenType.FloatLiteral => TypeKind.F64,
            TokenType.KeywordTrue or TokenType.KeywordFalse => TypeKind.Bool,
            TokenType.KeywordNone => TypeKind.NoneType,
            _ => TypeKind.Unknown
        };
    }

    public static string ToTypeString(this TypeKind kind)
    {
        return kind switch
        {
            TypeKind.I32 => "i32",
            TypeKind.F64 => "f64",
            TypeKind.Bool => "bool",
            TypeKind.Str => "str",
            TypeKind.Char => "char",
            TypeKind.NoneType => "NoneType",
            TypeKind.Any => "Any",
            _ => "Unknown"
        };
    }

    public static TypeKind FromString(string typeName)
    {
        return typeName switch
        {
            "i32" => TypeKind.I32,
            "f64" => TypeKind.F64,
            "bool" => TypeKind.Bool,
            "str" => TypeKind.Str,
            "char" => TypeKind.Char,
            "NoneType" => TypeKind.NoneType,
            "Any" => TypeKind.Any,
            _ => TypeKind.Unknown
        };
    }
}
```

---

### `Snek.Core\Ast\AstNode.cs`

```csharp
namespace Snek.Core.Ast;

public abstract record AstNode
{
    public AstNode? Parent { get; set; }

    public IEnumerable<AstNode> Ancestors()
    {
        AstNode? current = Parent;

        while (current is not null)
        {
            yield return current;
            current = current.Parent;
        }
    }

    public T? AncestorOfType<T>() where T : AstNode
    {
        return Ancestors()
            .OfType<T>()
            .FirstOrDefault();
    }
}
```

---

### `Snek.Core\Ast\BinaryExpressionNode.cs`

```csharp
namespace Snek.Core.Ast;

public record BinaryExpressionNode(ExpressionNode Left, Token Operator, ExpressionNode Right) : ExpressionNode;
```

---

### `Snek.Core\Ast\BreakStatementNode.cs`

```csharp
namespace Snek.Core.Ast;

public record BreakStatementNode : StatementNode;
```

---

### `Snek.Core\Ast\CallExpressionNode.cs`

```csharp
namespace Snek.Core.Ast;

public record CallExpressionNode(ExpressionNode Callee, List<ExpressionNode> Arguments) : ExpressionNode;
```

---

### `Snek.Core\Ast\ContinueStatementNode.cs`

```csharp
namespace Snek.Core.Ast;

public record ContinueStatementNode : StatementNode;
```

---

### `Snek.Core\Ast\DeclarationNode.cs`

```csharp
namespace Snek.Core.Ast;

public abstract record DeclarationNode : AstNode;
```

---

### `Snek.Core\Ast\DictExpressionNode.cs`

```csharp
namespace Snek.Core.Ast;

public record DictExpressionNode(List<(ExpressionNode Key, ExpressionNode Value)> Items) : ExpressionNode;
```

---

### `Snek.Core\Ast\ExpressionNode.cs`

```csharp
namespace Snek.Core.Ast;

public abstract record ExpressionNode : AstNode;
```

---

### `Snek.Core\Ast\ExpressionStatementNode.cs`

```csharp
namespace Snek.Core.Ast;

public record ExpressionStatementNode(ExpressionNode Expression) : StatementNode;
```

---

### `Snek.Core\Ast\FunctionDefNode.cs`

```csharp
using Snek.Core.Lexing;

namespace Snek.Core.Ast;

public record FunctionDefNode(
    Token Name,
    List<ParameterNode> Parameters,
    TypeNode? ReturnType,
    List<StatementNode> Body,
    int IndentLevel) : StatementNode;
```

---

### `Snek.Core\Ast\IdentifierExpressionNode.cs`

```csharp
using Snek.Core.Lexing;

namespace Snek.Core.Ast;

public record IdentifierExpressionNode(Token Name) : ExpressionNode;
```

---

### `Snek.Core\Ast\IfStatementNode.cs`

```csharp
namespace Snek.Core.Ast;

public record IfStatementNode(
    ExpressionNode Condition,
    List<StatementNode> ThenBody,
    List<StatementNode>? ElseBody,
    int IndentLevel) : StatementNode;
```

---

### `Snek.Core\Ast\IndexExpressionNode.cs`

```csharp
namespace Snek.Core.Ast;

public record IndexExpressionNode(ExpressionNode Target, ExpressionNode Index) : ExpressionNode;
```

---

### `Snek.Core\Ast\ListExpressionNode.cs`

```csharp
namespace Snek.Core.Ast;

public record ListExpressionNode(List<ExpressionNode> Elements) : ExpressionNode;
```

---

### `Snek.Core\Ast\LiteralExpressionNode.cs`

```csharp
using Snek.Core.Lexing;

namespace Snek.Core.Ast;

public record LiteralExpressionNode(Token Value) : ExpressionNode;
```

---

### `Snek.Core\Ast\MemberAccessExpressionNode.cs`

```csharp
using Snek.Core.Lexing;

namespace Snek.Core.Ast;

public record MemberAccessExpressionNode(ExpressionNode Object, Token Member) : ExpressionNode;
```

---

### `Snek.Core\Ast\ParameterNode.cs`

```csharp
using Snek.Core.Lexing;

namespace Snek.Core.Ast;

public record ParameterNode(Token Name, TypeNode? TypeAnnotation, ExpressionNode? Default) : AstNode;
```

---

### `Snek.Core\Ast\PassStatementNode.cs`

```csharp
namespace Snek.Core.Ast;

public record PassStatementNode : StatementNode;
```

---

### `Snek.Core\Ast\ProgramNode.cs`

```csharp
namespace Snek.Core.Ast;

public record ProgramNode(List<StatementNode> Statements) : AstNode;
```

---

### `Snek.Core\Ast\ReturnStatementNode.cs`

```csharp
namespace Snek.Core.Ast;

public record ReturnStatementNode(ExpressionNode? Value) : StatementNode;
```

---

### `Snek.Core\Ast\StatementNode.cs`

```csharp
namespace Snek.Core.Ast;

public abstract record StatementNode : AstNode;
```

---

### `Snek.Core\Ast\TypeNode.cs`

```csharp
using Snek.Core.Lexing;

namespace Snek.Core.Ast;

public record TypeNode(Token Name, List<TypeNode>? GenericArgs) : AstNode
{
    public static TypeNode Simple(Token name)
    {
        return new(name, null);
    }

    public static TypeNode Generic(Token name, List<TypeNode> args)
    {
        return new(name, args);
    }
}
```

---

### `Snek.Core\Ast\UnaryExpressionNode.cs`

```csharp
using Snek.Core.Lexing;

namespace Snek.Core.Ast;

public record UnaryExpressionNode(Token Operator, ExpressionNode Operand) : ExpressionNode;
```

---

### `Snek.Core\Ast\VariableDeclarationNode.cs`

```csharp
using Snek.Core.Lexing;

namespace Snek.Core.Ast;

public record VariableDeclarationNode(
    Token Name,
    TypeNode Type,
    ExpressionNode? Initializer,
    int IndentLevel
) : StatementNode;
```

---

### `Snek.Core\Ast\WhileStatementNode.cs`

```csharp
namespace Snek.Core.Ast;

public record WhileStatementNode(
    ExpressionNode Condition,
    List<StatementNode> Body,
    int IndentLevel) : StatementNode;
```

---

### `Snek.Core\Compiler\Assembler.cs`

```csharp
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Snek.Core.Compiler;

public sealed class Assembler
{
    private static string? LocateExecutable(string executableName)
    {
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrEmpty(pathEnv))
        {
            return null;
        }

        char separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
        string[] directories = pathEnv.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        foreach (string directory in directories)
        {
            string fullPath = Path.Combine(directory, executableName);

            if (!File.Exists(fullPath))
            {
                continue;
            }

            return fullPath;
        }

        return null;
    }

    public static bool Assemble(string asmPath, string outputDir)
    {
        string fasmExecutableName = OperatingSystem.IsWindows() ? "fasm.exe" : "fasm";
        string? fasmPath = LocateExecutable(fasmExecutableName);

        if (fasmPath == null)
        {
            Console.Error.WriteLine($"Error: FASM executable '{fasmExecutableName}' not found in PATH.");
            Console.Error.WriteLine("Please install Flat Assembler (FASM) from https://flatassembler.net/");
            Console.Error.WriteLine("Ensure the directory containing 'fasm' is added to your PATH environment variable.");
            return false;
        }

        try
        {
            Console.WriteLine("Executing FASM assembler...");

            ProcessStartInfo startInfo = CreateProcessStartInfo(fasmPath, asmPath, outputDir);
            SetIncludeEnvironmentVariable(startInfo, fasmPath);

            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                Console.Error.WriteLine("Failed to start FASM process.");
                return false;
            }

            string output = process.StandardOutput.ReadToEnd();
            string errors = process.StandardError.ReadToEnd();
            process.WaitForExit();

            PrintOutput(output, errors);

            if (process.ExitCode == 0)
            {
                Console.WriteLine("FASM execution successful.");
                return true;
            }
            else
            {
                Console.Error.WriteLine($"FASM execution failed with exit code {process.ExitCode}.");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error executing FASM: {ex.Message}");
            return false;
        }
    }

    private static ProcessStartInfo CreateProcessStartInfo(string fasmPath, string asmPath, string outputDir)
    {
        return new()
        {
            FileName = fasmPath,
            Arguments = $"\"{Path.GetFileName(asmPath)}\"",
            WorkingDirectory = outputDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private static void SetIncludeEnvironmentVariable(ProcessStartInfo startInfo, string fasmPath)
    {
        string? fasmDirectory = Path.GetDirectoryName(fasmPath);

        if (fasmDirectory == null)
        {
            return;
        }

        string includePath = Path.Combine(fasmDirectory, "INCLUDE");

        if (Directory.Exists(includePath))
        {
            startInfo.EnvironmentVariables["INCLUDE"] = includePath;
        }
    }

    private static void PrintOutput(string output, string errors)
    {
        if (!string.IsNullOrWhiteSpace(output))
        {
            Console.Write(output);
        }

        if (!string.IsNullOrWhiteSpace(errors))
        {
            Console.Error.Write(errors);
        }
    }
}
```

---

### `Snek.Core\Compiler\CompilerOptions.cs`

```csharp
namespace Snek.Core.Compiler;

public class CompilerOptions
{
    public string? OutputPath { get; set; }
    public string Syntax { get; set; } = "python";
    public bool Verbose { get; set; }
    public bool AsmOnly { get; set; }
}
```

---

### `Snek.Core\Compiler\CompilerService.cs`

```csharp
using Snek.Core.Analysis;
using Snek.Core.Diagnoistics;
using Snek.Core.Generation;
using Snek.Core.Lexing;
using Snek.Core.Parsing;
using Snek.Core.Pipeline;

namespace Snek.Core.Compiler;

public class CompilerService
{
    private readonly CompilerOptions _options;

    public CompilerService(CompilerOptions options)
    {
        _options = options;
    }

    public (bool Success, string? AssemblyPath, string? ExecutablePath) Compile(string sourcePath)
    {
        if (!File.Exists(sourcePath))
        {
            ReportFileNotFound(sourcePath);
            return (false, null, null);
        }

        string source = File.ReadAllText(sourcePath);
        CompilationResult result = RunCompilerPipeline(source, sourcePath);

        if (!result.Success)
        {
            PrintDiagnostics(source, sourcePath, result.Diagnostics);
            return (false, null, null);
        }

        string asmOutputPath = DetermineAssemblyOutputPath();
        WriteAssemblyFile(asmOutputPath, result.Output);

        if (_options.AsmOnly)
        {
            return (true, asmOutputPath, null);
        }

        bool assemblySucceeded = RunAssembler(asmOutputPath);
        if (assemblySucceeded)
        {
            string exeOutputPath = DetermineExecutableOutputPath();
            Console.WriteLine($"Executable created: {exeOutputPath}");
            return (true, asmOutputPath, exeOutputPath);
        }
        else
        {
            return (false, asmOutputPath, null);
        }
    }

    private CompilationResult RunCompilerPipeline(string source, string sourcePath)
    {
        PipelineOptions pipelineOptions = new()
        {
            EnableLogging = _options.Verbose
        };

        LexerRules lexerRules = DetermineLexerRules();
        Lexer lexer = new(lexerRules);
        Parser parser = new(lexerRules);
        SemanticAnalyzer analyzer = new();
        CodeGenerator generator = new();

        CompilerPipeline pipeline = new(lexer, parser, analyzer, generator, pipelineOptions);

        return pipeline.Compile(source, sourcePath);
    }

    private LexerRules DetermineLexerRules()
    {
        string syntax = _options.Syntax ?? string.Empty;

        return syntax.ToLowerInvariant() switch
        {
            "python" => LexerRules.CreatePythonStyle(),
            _ => new()
        };
    }

    private string DetermineAssemblyOutputPath()
    {
        return _options.OutputPath ?? "output.asm";
    }

    private string DetermineExecutableOutputPath()
    {
        return _options.OutputPath?.Replace(".asm", ".exe") ?? "output.exe";
    }

    private void WriteAssemblyFile(string asmOutputPath, string? assemblyContent)
    {
        File.WriteAllText(asmOutputPath, assemblyContent ?? string.Empty);
        Console.WriteLine($"Assembly generated: {asmOutputPath}");
    }

    private bool RunAssembler(string asmOutputPath)
    {
        string asmDirectory = Path.GetDirectoryName(Path.GetFullPath(asmOutputPath)) ?? ".";
        bool success = Assembler.Assemble(asmOutputPath, asmDirectory);

        if (!success)
        {
            Console.Error.WriteLine("Assembly failed. Check FASM output above.");
        }

        return success;
    }

    private static void ReportFileNotFound(string sourcePath)
    {
        Console.Error.WriteLine($"Error: Input file not found: {sourcePath}");
    }

    private static void PrintDiagnostics(string source, string sourcePath, IReadOnlyList<Diagnostic> diagnostics)
    {
        string[] sourceLines = source.ReplaceLineEndings("\n").Split('\n');

        Dictionary<string, string[]> sourceFiles = new()
        {
            [sourcePath] = sourceLines
        };

        IReadOnlyList<Diagnostic> uniqueDiagnostics = DeduplicateDiagnostics(diagnostics);
        DiagnosticPrinter printer = new(uniqueDiagnostics, sourceFiles);

        printer.Print();
    }

    private static IReadOnlyList<Diagnostic> DeduplicateDiagnostics(IReadOnlyList<Diagnostic> diagnostics)
    {
        List<Diagnostic> deduplicated = [];
        HashSet<(string Source, int Line)> seen = [];

        foreach (Diagnostic diagnostic in diagnostics
            .OrderBy(d => d.SourceName)
            .ThenBy(d => d.Line)
            .ThenBy(d => d.Column))
        {
            if (seen.Add((diagnostic.SourceName, diagnostic.Line)))
            {
                deduplicated.Add(diagnostic);
            }
        }

        return deduplicated;
    }
}
```

---

### `Snek.Core\Diagnoistics\Diagnostic.cs`

```csharp
namespace Snek.Core.Diagnoistics;

public record Diagnostic(
    string SourceName,
    string Message,
    int Line,
    int Column,
    DiagnosticSeverity Severity = DiagnosticSeverity.Error,
    int Length = 1)
{
    public bool IsError => Severity == DiagnosticSeverity.Error;
    public bool IsWarning => Severity == DiagnosticSeverity.Warning;

    public override string ToString()
    {
        string prefix = Severity == DiagnosticSeverity.Error
            ? "error"
            : "warning";

        return $"{SourceName}({Line},{Column}): {prefix}: {Message}";
    }
}
```

---

### `Snek.Core\Diagnoistics\DiagnosticPrinter.cs`

```csharp
namespace Snek.Core.Diagnoistics;

public class DiagnosticPrinter
{
    private readonly IReadOnlyList<Diagnostic> _diagnostics;
    private readonly IReadOnlyDictionary<string, string[]> _sourceFiles;

    public DiagnosticPrinter(IReadOnlyList<Diagnostic> diagnostics, IReadOnlyDictionary<string, string[]> sourceFiles)
    {
        _diagnostics = diagnostics;
        _sourceFiles = sourceFiles;
    }

    public void Print()
    {
        foreach (Diagnostic diagnostic in _diagnostics
            .OrderBy(d => d.SourceName)
            .ThenBy(d => d.Line)
            .ThenBy(d => d.Column))
        {
            if (!_sourceFiles.TryGetValue(diagnostic.SourceName, out string[]? lines) || diagnostic.Line < 1)
            {
                Console.Error.WriteLine(
                    $"{diagnostic.SourceName}({diagnostic.Line},{diagnostic.Column}): " +
                    $"{(diagnostic.IsError ? "error" : "warning")}: {diagnostic.Message}");
                continue;
            }

            Console.Error.WriteLine();

            ConsoleColor color = diagnostic.Severity switch
            {
                DiagnosticSeverity.Warning => ConsoleColor.Yellow,
                _ => ConsoleColor.Red
            };

            string prefix = diagnostic.Severity switch
            {
                DiagnosticSeverity.Warning => "Warning: ",
                _ => "Error: "
            };

            Console.ForegroundColor = color;
            Console.Error.Write(prefix);
            Console.ResetColor();
            Console.Error.WriteLine(diagnostic.Message);

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Error.WriteLine($"  --> {diagnostic.SourceName}:{diagnostic.Line}:{diagnostic.Column}");
            Console.Error.WriteLine("   |");

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Error.Write($"{diagnostic.Line,2} | ");
            Console.ResetColor();

            string line = lines[diagnostic.Line - 1];
            Console.Error.WriteLine(line);

            Console.ForegroundColor = ConsoleColor.DarkGray;
            int pad = int.Max(0, diagnostic.Column - 1);
            Console.Error.Write("   | ");
            Console.ForegroundColor = color;
            Console.Error.WriteLine(
                new string(' ', pad) + new string('^', diagnostic.Length));
            Console.ResetColor();
        }
    }
}
```

---

### `Snek.Core\Diagnoistics\DiagnosticSeverity.cs`

```csharp
namespace Snek.Core.Diagnoistics;

public enum DiagnosticSeverity
{
    Error,
    Warning
}
```

---

### `Snek.Core\Generation\BuiltinFunctionEmitter.cs`

```csharp
namespace Snek.Core.Generation;

public class BuiltinFunctionEmitter
{
    private readonly GenerationContext _generationContext;
    private readonly ExpressionEmitter _expressionEmitter;

    public BuiltinFunctionEmitter(GenerationContext generationContext, ExpressionEmitter expressionEmitter)
    {
        _generationContext = generationContext;
        _expressionEmitter = expressionEmitter;
    }

    public bool TryEmitBuiltin(CallExpressionNode call)
    {
        string calleeName = ExtractCalleeName(call);

        switch (calleeName)
        {
            case "print":
                EmitPrintCall(call);
                return true;
            case "pause":
                EmitPauseCall();
                return true;
            default:
                return false;
        }
    }

    private string ExtractCalleeName(CallExpressionNode call)
    {
        return call.Callee is IdentifierExpressionNode identifier
            ? identifier.Name.Value
            : string.Empty;
    }

    private void EmitPauseCall()
    {
        _generationContext.Emit("call [_getch]");
    }

    private void EmitPrintCall(CallExpressionNode call)
    {
        if (call.Arguments.Count == 0)
        {
            EmitPlainNewline();
            return;
        }

        ExpressionNode firstArgument = call.Arguments[0];

        if (IsStringLiteral(firstArgument))
        {
            EmitStringLiteralPrint(firstArgument);
        }
        else
        {
            EmitFormattedPrint(firstArgument);
        }

        _generationContext.Emit("push eax");
    }

    private void EmitPlainNewline()
    {
        string formatLabel = _generationContext.EnsureFormatString("\n");
        _generationContext.Emit($"push {formatLabel}");
        _generationContext.Emit("call [printf]");
        _generationContext.Emit("add esp, 4");
        _generationContext.Emit("push eax");
    }

    private void EmitStringLiteralPrint(ExpressionNode stringExpression)
    {
        _expressionEmitter.Emit(stringExpression);
        _generationContext.Emit("call [printf]");
        _generationContext.Emit("add esp, 4");
    }

    private void EmitFormattedPrint(ExpressionNode valueExpression)
    {
        string formatLabel = _generationContext.EnsureFormatString("%d\n");
        _expressionEmitter.Emit(valueExpression);
        _generationContext.Emit($"push {formatLabel}");
        _generationContext.Emit("call [printf]");
        _generationContext.Emit("add esp, 8");
    }

    private static bool IsStringLiteral(ExpressionNode expression)
    {
        return expression is LiteralExpressionNode literal
            && literal.Value.Type == TokenType.StringLiteral;
    }
}
```

---

### `Snek.Core\Generation\CodeGenerator.cs`

```csharp
using Snek.Core.Ast;
using Snek.Core.Pipeline;

namespace Snek.Core.Generation;

public class CodeGenerator : ICodeGenerator
{
    private readonly GenerationContext _generationContext = new();
    private SectionEmitter _sectionEmitter = null!;
    private StringCollector _stringCollector = null!;
    private ExpressionEmitter _expressionEmitter = null!;
    private StatementEmitter _statementEmitter = null!;

    public string? Generate(AstNode root, CompilationContext context)
    {
        InitializeComponents();

        if (root is not ProgramNode program)
        {
            return null;
        }

        _stringCollector.Collect(program);

        EmitPrelude();
        EmitFunctionDefinitions(program);
        EmitEntryPoint(program);

        return _generationContext.Output.ToString();
    }

    private void InitializeComponents()
    {
        _generationContext.Reset();
        _sectionEmitter = new(_generationContext);
        _stringCollector = new(_generationContext);
        _expressionEmitter = new(_generationContext);
        _statementEmitter = new(_generationContext, _expressionEmitter);
    }

    private void EmitPrelude()
    {
        _sectionEmitter.EmitHeader();
        _sectionEmitter.EmitDataSection();
        _sectionEmitter.EmitImportSection();
        _sectionEmitter.EmitTextSectionHeader();
        _sectionEmitter.EmitEntryPoint();
    }

    private void EmitFunctionDefinitions(ProgramNode program)
    {
        foreach (StatementNode statement in program.Statements)
        {
            if (statement is not FunctionDefNode function)
            {
                continue;
            }

            _statementEmitter.EmitFunction(function);
        }
    }

    private void EmitEntryPoint(ProgramNode program)
    {
        _statementEmitter.EmitEntryPoint(program.Statements);
    }
}
```

---

### `Snek.Core\Generation\ExpressionEmitter.cs`

```csharp
using Snek.Core.Ast;

namespace Snek.Core.Generation;

public class ExpressionEmitter
{
    private readonly GenerationContext _generationContext;
    private readonly BuiltinFunctionEmitter _builtinEmitter;

    public ExpressionEmitter(GenerationContext generationContext)
    {
        _generationContext = generationContext;
        _builtinEmitter = new BuiltinFunctionEmitter(generationContext, this);
    }

    public void Emit(ExpressionNode expression)
    {
        switch (expression)
        {
            case LiteralExpressionNode literal:
                EmitLiteralValue(literal);
                break;

            case IdentifierExpressionNode identifier:
                EmitIdentifierAccess(identifier);
                break;

            case CallExpressionNode call:
                EmitFunctionCall(call);
                break;

            case BinaryExpressionNode binary:
                EmitBinaryOperation(binary);
                break;

            default:
                _generationContext.Emit("; unsupported expression");
                break;
        }
    }

    private void EmitLiteralValue(LiteralExpressionNode literal)
    {
        switch (literal.Value.Type)
        {
            case TokenType.StringLiteral:
                string label = _generationContext.StringLiterals.First(kvp => kvp.Value == literal.Value.Value).Key;
                _generationContext.Emit($"push {label}");
                break;

            case TokenType.IntegerLiteral:
                _generationContext.Emit($"push {literal.Value.Value}");
                break;

            case TokenType.KeywordTrue:
                _generationContext.Emit("push 1");
                break;

            case TokenType.KeywordFalse:
                _generationContext.Emit("push 0");
                break;
        }
    }

    private void EmitIdentifierAccess(IdentifierExpressionNode identifier)
    {
        if (_generationContext.LocalOffsets.TryGetValue(identifier.Name.Value, out int offset))
        {
            _generationContext.Emit($"; load {identifier.Name.Value} (ebp-{offset})");
            _generationContext.Emit($"mov eax, [ebp-{offset}]");
            _generationContext.Emit("push eax");
        }
        else
        {
            _generationContext.Emit($"; load {identifier.Name.Value} (global/undefined)");
            _generationContext.Emit("push 0");
        }
    }

    private void EmitFunctionCall(CallExpressionNode call)
    {
        if (_builtinEmitter.TryEmitBuiltin(call))
        {
            return;
        }

        string calleeName = ExtractCalleeName(call);
        string callTarget = DetermineCallTarget(calleeName);

        EmitArgumentsRightToLeft(call);
        _generationContext.Emit($"call {callTarget}");
        CleanupStackAfterCall(call.Arguments.Count);
        _generationContext.Emit("push eax");
    }

    private string ExtractCalleeName(CallExpressionNode call)
    {
        return call.Callee is IdentifierExpressionNode identifier
            ? identifier.Name.Value
            : "unknown";
    }

    private string DetermineCallTarget(string calleeName)
    {
        if (_generationContext.ExternalFunctions.Contains(calleeName))
        {
            return $"[{calleeName}]";
        }

        return _generationContext.MangleName(calleeName);
    }

    private void EmitArgumentsRightToLeft(CallExpressionNode call)
    {
        for (int i = call.Arguments.Count - 1; i >= 0; i--)
        {
            Emit(call.Arguments[i]);
        }
    }

    private void CleanupStackAfterCall(int argumentCount)
    {
        if (argumentCount <= 0)
        {
            return;
        }

        _generationContext.Emit($"add esp, {argumentCount * 4}");
    }

    private void EmitBinaryOperation(BinaryExpressionNode binary)
    {
        Emit(binary.Right);
        Emit(binary.Left);

        _generationContext.Emit("pop ebx");
        _generationContext.Emit("pop eax");

        switch (binary.Operator.Type)
        {
            case TokenType.Plus:
                _generationContext.Emit("add eax, ebx");
                break;

            case TokenType.Minus:
                _generationContext.Emit("sub eax, ebx");
                break;

            case TokenType.Star:
                _generationContext.Emit("imul eax, ebx");
                break;

            case TokenType.DoubleEquals:
                _generationContext.Emit("cmp eax, ebx");
                _generationContext.Emit("sete al");
                _generationContext.Emit("movzx eax, al");
                break;

            default:
                _generationContext.Emit("; unsupported binary op");
                break;
        }

        _generationContext.Emit("push eax");
    }
}
```

---

### `Snek.Core\Generation\GenerationContext.cs`

```csharp
using System.Text;

namespace Snek.Core.Generation;

public class GenerationContext
{
    private const string Indent = "    ";

    private static readonly HashSet<string> X86Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        // General-purpose instructions
        "mov", "push", "pop", "lea", "xchg", "nop",

        // Integer arithmetic
        "add", "adc", "sub", "sbb", "inc", "dec", "neg",
        "mul", "imul", "div", "idiv",
        
        // Bitwise / logical
        "and", "or", "xor", "not", "test",
        
        // Shift / rotate
        "shl", "shr", "sal", "sar", "rol", "ror", "rcl", "rcr",
        
        // Comparison
        "cmp",
        
        // Control flow
        "jmp", "call", "ret", "retn", "retf", "int", "iret", "loop", "pause",
        "je", "jne", "jz", "jnz", "ja", "jb", "jg", "jl", "jge", "jle",
        
        // Conditional set
        "sete", "setne", "setz", "setnz",
        
        // Data movement
        "cbw", "cwd", "cdq", "cwde", "movsx", "movzx",
        
        // Stack
        "pusha", "popa", "pushad", "popad", "pushf", "popf",
        "enter", "leave",
        
        // String
        "movs", "cmps", "scas", "lods", "stos",
        
        // FASM directives that clash
        "format", "entry", "include", "section",
        "library", "import", "align",
        "db", "dw", "dd", "dq", "dt", "rb", "rw", "rd", "rq",
    };

    public StringBuilder Output { get; } = new();
    public Stack<string> LabelStack { get; } = new();
    public Dictionary<string, string> StringLiterals { get; } = [];
    public HashSet<string> ExternalFunctions { get; } = [];
    public int LabelCounter { get; set; }
    public int StringCounter { get; set; }
    public Dictionary<string, int> LocalOffsets { get; } = [];
    public int NextLocalOffset { get; set; } = 4;

    public void Reset()
    {
        Output.Clear();
        LabelStack.Clear();
        StringLiterals.Clear();
        ExternalFunctions.Clear();
        LabelCounter = 0;
        StringCounter = 0;
        LocalOffsets.Clear();
        NextLocalOffset = 4;
    }

    public string MangleName(string name)
    {
        return X86Reserved.Contains(name)
            ? $"_{name}"
            : name;
    }

    public void Emit(string instruction)
    {
        Output.Append(Indent);
        Output.AppendLine(instruction);
    }

    public void EmitLine(string text = "")
    {
        Output.AppendLine(text);
    }

    public string EnsureFormatString(string format)
    {
        foreach (KeyValuePair<string, string> kvp in StringLiterals)
        {
            if (kvp.Value == format)
            {
                return kvp.Key;
            }
        }

        string label = $"fmt{StringCounter++}";
        StringLiterals[label] = format;
        return label;
    }
}
```

---

### `Snek.Core\Generation\ICodeGenerator.cs`

```csharp
using Snek.Core.Ast;
using Snek.Core.Pipeline;

namespace Snek.Core.Generation;

public interface ICodeGenerator
{
    string? Generate(AstNode root, CompilationContext context);
}
```

---

### `Snek.Core\Generation\SectionEmitter.cs`

```csharp
namespace Snek.Core.Generation;

public class SectionEmitter
{
    private readonly GenerationContext _ctx;

    public SectionEmitter(GenerationContext ctx)
    {
        _ctx = ctx;
    }

    public void EmitHeader()
    {
        _ctx.EmitLine("format PE console");
        _ctx.EmitLine("entry start");
        _ctx.EmitLine();
        _ctx.EmitLine("include 'win32a.inc'");
        _ctx.EmitLine();
    }

    public void EmitDataSection()
    {
        if (_ctx.StringLiterals.Count == 0)
        {
            return;
        }

        _ctx.EmitLine("section '.data' data readable writeable");

        foreach ((string? label, string? value) in _ctx.StringLiterals)
        {
            string encoded = EncodeStringLiteral(value);
            _ctx.Emit($"{label} db {encoded}");
        }

        _ctx.EmitLine();
    }

    public void EmitImportSection()
    {
        _ctx.EmitLine("section '.idata' import data readable");
        _ctx.EmitLine();

        Dictionary<string, HashSet<string>> libs = BuildImportLibrary();

        _ctx.Emit($"library {FormatLibraryDefinitions(libs)}");
        _ctx.EmitLine();

        foreach ((string? libName, HashSet<string>? functions) in libs.OrderBy(k => k.Key))
        {
            _ctx.Emit($"import {FormatImportLine(libName, functions)}");
        }

        _ctx.EmitLine();
    }

    public void EmitTextSectionHeader()
    {
        _ctx.EmitLine("section '.text' code readable executable");
        _ctx.EmitLine();
    }

    public void EmitEntryPoint()
    {
        _ctx.EmitLine("start:");
        _ctx.Emit("call _start");
        _ctx.Emit("push eax");
        _ctx.Emit("call [ExitProcess]");
        _ctx.EmitLine();
    }

    private static string EncodeStringLiteral(string value)
    {
        List<string> parts = [];
        bool inQuoted = false;

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (NeedsByteEncoding(c))
            {
                // Close any open quoted segment
                if (inQuoted)
                {
                    parts[^1] += "'";
                    inQuoted = false;
                }
                // Add the numeric byte value
                parts.Add(((byte)c).ToString());
            }
            else
            {
                if (!inQuoted)
                {
                    parts.Add("'");
                    inQuoted = true;
                }
                parts[^1] += c;
            }
        }

        // Close final quoted segment if open
        if (inQuoted)
        {
            parts[^1] += "'";
        }

        // Add null terminator
        parts.Add("0");

        return string.Join(",", parts);
    }

    private static bool NeedsByteEncoding(char c)
    {
        // Characters that must be encoded as numeric bytes: newline, tab, carriage return, quote, backslash, etc.
        return c is '\n' or '\t' or '\r' or '\'' or '"' or '\\';
    }

    private Dictionary<string, HashSet<string>> BuildImportLibrary()
    {
        Dictionary<string, HashSet<string>> libs = new()
        {
            ["kernel32.dll"] = ["ExitProcess"],
            ["msvcrt.dll"] = ["printf"]
        };

        foreach (string func in _ctx.ExternalFunctions)
        {
            libs["msvcrt.dll"].Add(func);
        }

        return libs;
    }

    private static string FormatLibraryDefinitions(Dictionary<string, HashSet<string>> libs)
    {
        IEnumerable<string> defs = libs.Keys
            .Select(lib => $"{lib.Split('.')[0]},'{lib}'");

        return string.Join(",", defs);
    }

    private static string FormatImportLine(string libName, HashSet<string> functions)
    {
        string alias = libName.Split('.')[0];

        IEnumerable<string> imports = functions
            .OrderBy(f => f)
            .Select(f => $"{f},'{f}'");

        return $"{alias}, {string.Join(",", imports)}";
    }
}
```

---

### `Snek.Core\Generation\StatementEmitter.cs`

```csharp
using Snek.Core.Ast;

namespace Snek.Core.Generation;

public class StatementEmitter
{
    private readonly GenerationContext _generationContext;
    private readonly ExpressionEmitter _expressionEmitter;

    public StatementEmitter(GenerationContext generationContext, ExpressionEmitter expressionEmitter)
    {
        _generationContext = generationContext;
        _expressionEmitter = expressionEmitter;
    }

    public void EmitEntryPoint(IReadOnlyList<StatementNode> statements)
    {
        List<StatementNode> topLevelStatements = statements
            .Where(statement => statement is not FunctionDefNode)
            .ToList();

        if (topLevelStatements.Count == 0)
        {
            EmitEmptyEntryPointStub();
            return;
        }

        BeginFunctionPrologue();
        ReserveLocalVariablesSpace(topLevelStatements);
        EmitStatements(topLevelStatements);
        EmitFunctionEpilogueWithZeroReturn();
    }

    public void EmitFunction(FunctionDefNode function)
    {
        string mangledName = _generationContext.MangleName(function.Name.Value);
        _generationContext.EmitLine($"{mangledName}:");
        BeginFunctionPrologue();

        EmitParameterComments(function.Parameters);
        EmitStatements(function.Body);

        if (function.ReturnType == null)
        {
            _generationContext.Emit("xor eax, eax");
        }

        EmitFunctionEpilogue();
    }

    public void Emit(StatementNode statement)
    {
        switch (statement)
        {
            case ExpressionStatementNode expressionStatement:
                EmitExpressionStatement(expressionStatement);
                break;
            case ReturnStatementNode returnStatement:
                EmitReturnStatement(returnStatement);
                break;
            case IfStatementNode ifStatement:
                EmitIfStatement(ifStatement);
                break;
            case WhileStatementNode whileStatement:
                EmitWhileStatement(whileStatement);
                break;
            case VariableDeclarationNode variableDeclaration:
                EmitVariableDeclaration(variableDeclaration);
                break;
            default:
                _generationContext.Emit("; unsupported statement");
                break;
        }
    }

    private void EmitEmptyEntryPointStub()
    {
        _generationContext.EmitLine("_start:");
        _generationContext.Emit("xor eax, eax");
        _generationContext.Emit("ret");
        _generationContext.EmitLine();
    }

    private void BeginFunctionPrologue()
    {
        _generationContext.Emit("push ebp");
        _generationContext.Emit("mov ebp, esp");
    }

    private void EmitFunctionEpilogueWithZeroReturn()
    {
        _generationContext.Emit("xor eax, eax");
        EmitFunctionEpilogue();
    }

    private void EmitFunctionEpilogue()
    {
        _generationContext.Emit("leave");
        _generationContext.Emit("ret");
        _generationContext.EmitLine();
    }

    private void ReserveLocalVariablesSpace(List<StatementNode> topLevelStatements)
    {
        int localsSize = ComputeLocalVariablesSize(topLevelStatements);
        if (localsSize > 0)
        {
            _generationContext.Emit($"sub esp, {localsSize}");
        }
    }

    private int ComputeLocalVariablesSize(IEnumerable<StatementNode> statements)
    {
        int size = 0;
        foreach (StatementNode statement in statements)
        {
            if (statement is VariableDeclarationNode)
            {
                size += 4;
            }
        }
        return size;
    }

    private void EmitStatements(IEnumerable<StatementNode> statements)
    {
        foreach (StatementNode statement in statements)
        {
            Emit(statement);
        }
    }

    private void EmitParameterComments(IEnumerable<ParameterNode> parameters)
    {
        int paramOffset = 8;
        foreach (ParameterNode parameter in parameters)
        {
            _generationContext.Emit($"; param {parameter.Name.Value} at [ebp+{paramOffset}]");
            paramOffset += 4;
        }
    }

    private void EmitExpressionStatement(ExpressionStatementNode expressionStatement)
    {
        _expressionEmitter.Emit(expressionStatement.Expression);
        _generationContext.Emit("pop eax");
    }

    private void EmitReturnStatement(ReturnStatementNode returnStatement)
    {
        if (returnStatement.Value == null)
        {
            _generationContext.Emit("xor eax, eax");
        }
        else
        {
            _expressionEmitter.Emit(returnStatement.Value);
        }
    }

    private void EmitIfStatement(IfStatementNode ifStatement)
    {
        string elseLabel = $"_else_{_generationContext.LabelCounter++}";
        string endLabel = $"_endif_{_generationContext.LabelCounter}";

        _expressionEmitter.Emit(ifStatement.Condition);
        _generationContext.Emit("pop eax");
        _generationContext.Emit("test eax, eax");
        _generationContext.Emit($"jz {elseLabel}");

        EmitStatements(ifStatement.ThenBody);
        _generationContext.Emit($"jmp {endLabel}");
        _generationContext.EmitLine($"{elseLabel}:");

        if (ifStatement.ElseBody != null)
        {
            EmitStatements(ifStatement.ElseBody);
        }

        _generationContext.EmitLine($"{endLabel}:");
    }

    private void EmitWhileStatement(WhileStatementNode whileStatement)
    {
        string startLabel = $"_while_{_generationContext.LabelCounter}";
        string endLabel = $"_endwhile_{_generationContext.LabelCounter++}";

        _generationContext.EmitLine($"{startLabel}:");
        _expressionEmitter.Emit(whileStatement.Condition);
        _generationContext.Emit("pop eax");
        _generationContext.Emit("test eax, eax");
        _generationContext.Emit($"jz {endLabel}");

        EmitStatements(whileStatement.Body);
        _generationContext.Emit($"jmp {startLabel}");
        _generationContext.EmitLine($"{endLabel}:");
    }

    private void EmitVariableDeclaration(VariableDeclarationNode variableDeclaration)
    {
        if (variableDeclaration.Initializer != null)
        {
            _expressionEmitter.Emit(variableDeclaration.Initializer);
        }
        else
        {
            _generationContext.Emit("xor eax, eax");
            _generationContext.Emit("push eax");
        }

        int offset = _generationContext.NextLocalOffset;
        _generationContext.LocalOffsets[variableDeclaration.Name.Value] = offset;
        _generationContext.NextLocalOffset += 4;

        _generationContext.Emit("pop eax");
        _generationContext.Emit($"mov [ebp-{offset}], eax");
    }
}
```

---

### `Snek.Core\Generation\StringCollector.cs`

```csharp
using Snek.Core.Ast;
using Snek.Core.Lexing;
using System.Reflection;

namespace Snek.Core.Generation;

public class StringCollector
{
    private readonly GenerationContext _ctx;

    public StringCollector(GenerationContext ctx)
    {
        _ctx = ctx;
    }

    public void Collect(AstNode node)
    {
        CollectNode(node);
        WalkChildren(node);
    }

    private void CollectNode(AstNode node)
    {
        if (node is LiteralExpressionNode lit)
        {
            CollectStringLiteral(lit);
        }
        else if (node is CallExpressionNode call)
        {
            CollectExternalCall(call);
        }
    }

    private void CollectStringLiteral(LiteralExpressionNode lit)
    {
        if (lit.Value.Type != TokenType.StringLiteral)
        {
            return;
        }

        if (_ctx.StringLiterals.ContainsValue(lit.Value.Value))
        {
            return;
        }

        _ctx.StringLiterals[$"str{_ctx.StringCounter++}"] = lit.Value.Value;
    }

    private void CollectExternalCall(CallExpressionNode call)
    {
        if (call.Callee is not IdentifierExpressionNode id)
        {
            return;
        }

        if (id.Name.Value is "print")
        {
            _ctx.ExternalFunctions.Add("printf");

            // Check if we need format strings for non-string arguments
            if (call.Arguments.Count > 0 && !IsStringLiteral(call.Arguments[0]))
            {
                // Ensure integer format string is collected
                if (!_ctx.StringLiterals.ContainsValue("%d\n"))
                {
                    _ctx.StringLiterals[$"fmt{_ctx.StringCounter++}"] = "%d\n";
                }
            }
            else if (call.Arguments.Count == 0)
            {
                // Empty print - just newline
                if (!_ctx.StringLiterals.ContainsValue("\n"))
                {
                    _ctx.StringLiterals[$"fmt{_ctx.StringCounter++}"] = "\n";
                }
            }

            return;
        }

        if (id.Name.Value is "pause")
        {
            _ctx.ExternalFunctions.Add("_getch");
            return;
        }

        _ctx.ExternalFunctions.Add(id.Name.Value);
    }

    private bool IsStringLiteral(ExpressionNode expr)
    {
        return expr is LiteralExpressionNode lit
            && lit.Value.Type == TokenType.StringLiteral;
    }

    private void WalkChildren(AstNode node)
    {
        foreach (PropertyInfo prop in node.GetType().GetProperties())
        {
            if (prop.Name == "Parent")
            {
                continue;
            }

            object? value = prop.GetValue(node);

            if (value is AstNode child)
            {
                Collect(child);
            }
            else if (value is IEnumerable<AstNode> children)
            {
                foreach (AstNode c in children)
                {
                    Collect(c);
                }
            }
        }
    }
}
```

---

### `Snek.Core\Lexing\ILexer.cs`

```csharp
namespace Snek.Core.Lexing;

public interface ILexer
{
    IEnumerable<Token> Tokenize(string source, CompilationContext context);
}
```

---

### `Snek.Core\Lexing\Lexer.cs`

```csharp
using Snek.Core.Pipeline;
using System.Text;

namespace Snek.Core.Lexing;

public class Lexer : ILexer
{
    private readonly LexerRules _rules;
    private string _source = string.Empty;
    private int _position;
    private int _line = 1;
    private int _column = 1;
    private CompilationContext? _context;
    private readonly Stack<int> _indentStack = new();

    public Lexer(LexerRules? rules = null)
    {
        _rules = rules ?? new();
    }

    public IEnumerable<Token> Tokenize(string source, CompilationContext context)
    {
        _source = source;
        _position = 0;
        _line = 1;
        _column = 1;
        _context = context;
        _indentStack.Clear();
        _indentStack.Push(0);

        List<Token> tokens = [];

        while (!IsAtEnd())
        {
            SkipWhitespaceAndComments();
            if (IsAtEnd())
            {
                break;
            }

            int startLine = _line;
            int startColumn = _column;

            if (TryReadKeywordOrIdentifier(tokens))
            {
                continue;
            }

            if (TryReadNumber(tokens))
            {
                continue;
            }

            if (TryReadString(tokens))
            {
                continue;
            }

            if (TryReadOperator(tokens))
            {
                continue;
            }

            if (TryReadStructural(tokens))
            {
                continue;
            }

            ReportError($"Unexpected character '{Peek()}'", startLine, startColumn);
            Advance();
        }

        // Emit dedents to close all indentation levels
        while (_indentStack.Count > 1)
        {
            _indentStack.Pop();
            tokens.Add(new Token(TokenType.Dedent, "", _line, _column));
        }

        tokens.Add(new Token(TokenType.Eof, "", _line, _column));
        return tokens;
    }

    private bool IsAtEnd()
    {
        return _position >= _source.Length;
    }

    private char Peek(int offset = 0)
    {
        return _position + offset < _source.Length
            ? _source[_position + offset]
            : '\0';
    }

    private char Advance()
    {
        char c = _source[_position++];

        if (c == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }

        return c;
    }

    private void SkipWhitespaceAndComments()
    {
        while (!IsAtEnd())
        {
            char c = Peek();

            if (char.IsWhiteSpace(c) && c != '\n')
            {
                Advance(); continue;
            }

            if (c == '#')
            {
                while (!IsAtEnd() && Peek() != '\n')
                {
                    Advance();
                }
                continue;
            }

            break;
        }
    }

    private bool TryReadKeywordOrIdentifier(List<Token> tokens)
    {
        if (!char.IsLetter(Peek()) && Peek() != '_' && !_rules.IdentifierStartChars.Contains(Peek()))
        {
            return false;
        }

        int startLine = _line;
        int startColumn = _column;
        StringBuilder sb = new();

        while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '_' || _rules.IdentifierContinueChars.Contains(Peek())))
        {
            sb.Append(Advance());
        }

        string value = sb.ToString();
        TokenType type = _rules.Keywords.TryGetValue(value, out TokenType keywordType) ? keywordType : TokenType.Identifier;
        tokens.Add(new Token(type, value, startLine, startColumn));
        return true;
    }

    private bool TryReadNumber(List<Token> tokens)
    {
        if (!char.IsDigit(Peek()))
        {
            return false;
        }

        int startLine = _line;
        int startColumn = _column;
        StringBuilder sb = new();
        bool isFloat = false;

        // Integer part
        while (char.IsDigit(Peek()))
        {
            sb.Append(Advance());
        }

        // Fractional part
        if (Peek() == '.' && char.IsDigit(Peek(1)))
        {
            isFloat = true;
            sb.Append(Advance()); // .
            while (char.IsDigit(Peek()))
            {
                sb.Append(Advance());
            }
        }

        // Exponent
        if (Peek() is 'e' or 'E')
        {
            isFloat = true;
            sb.Append(Advance());
            if (Peek() is '+' or '-')
            {
                sb.Append(Advance());
            }

            while (char.IsDigit(Peek()))
            {
                sb.Append(Advance());
            }
        }

        string value = sb.ToString();
        TokenType type = isFloat ? TokenType.FloatLiteral : TokenType.IntegerLiteral;
        tokens.Add(new Token(type, value, startLine, startColumn));
        return true;
    }

    private bool TryReadString(List<Token> tokens)
    {
        char c = Peek();
        if (c != _rules.StringDelimiter && c != _rules.CharDelimiter)
        {
            return false;
        }

        int startLine = _line;
        int startColumn = _column;
        char delimiter = Advance(); // consume opening quote
        bool isChar = delimiter == _rules.CharDelimiter;
        StringBuilder sb = new();

        while (!IsAtEnd() && Peek() != delimiter)
        {
            char ch = Advance();

            if (ch == '\\')
            {
                if (IsAtEnd())
                {
                    break;
                }

                char escaped = Advance();
                sb.Append(escaped switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    '\\' => '\\',
                    '"' => '"',
                    '\'' => '\'',
                    _ => escaped
                });
            }
            else
            {
                sb.Append(ch);
            }
        }

        if (IsAtEnd() || Peek() != delimiter)
        {
            ReportError("Unterminated string literal", startLine, startColumn);
            return true;
        }
        Advance(); // consume closing quote

        TokenType type = isChar ? TokenType.CharLiteral : TokenType.StringLiteral;
        tokens.Add(new Token(type, sb.ToString(), startLine, startColumn));
        return true;
    }

    private bool TryReadOperator(List<Token> tokens)
    {
        // Try longest operators first
        foreach ((string? pattern, TokenType type) in _rules.Operators.OrderByDescending(o => o.Pattern.Length))
        {
            if (!MatchString(pattern))
            {
                continue;
            }

            int startLine = _line;
            int startColumn = _column;
            // Advance past the matched pattern
            for (int i = 0; i < pattern.Length; i++)
            {
                Advance();
            }

            tokens.Add(new Token(type, pattern, startLine, startColumn));
            return true;
        }
        return false;
    }

    private bool TryReadStructural(List<Token> tokens)
    {
        char c = Peek();
        int startLine = _line;
        int startColumn = _column;

        if (c == '\n')
        {
            Advance();
            HandleNewline(tokens, startLine, startColumn);
            return true;
        }

        // Single-char structural tokens not in operators list
        if (c is '(' or ')' or '[' or ']' or '{' or '}' or ',' or '.' or ':')
        {
            Advance();
            TokenType type = c switch
            {
                '(' => TokenType.LeftParen,
                ')' => TokenType.RightParen,
                '[' => TokenType.LeftBracket,
                ']' => TokenType.RightBracket,
                '{' => TokenType.LeftBrace,
                '}' => TokenType.RightBrace,
                ',' => TokenType.Comma,
                '.' => TokenType.Dot,
                ':' => TokenType.Colon,
                _ => TokenType.Unknown
            };
            tokens.Add(new Token(type, c.ToString(), startLine, startColumn));
            return true;
        }

        return false;
    }

    private void HandleNewline(List<Token> tokens, int line, int column)
    {
        if (!_rules.SupportsIndentation)
        {
            tokens.Add(new Token(TokenType.Newline, "", line, column));
            return;
        }

        // Always emit Newline first, before any Indent/Dedent tokens.
        // This ensures the parser sees "Statement -> Newline -> Indent -> Block".
        tokens.Add(new Token(TokenType.Newline, "", line, column));

        // Calculate indentation of next non-empty line
        int indent = 0;
        int tempPos = _position;
        while (tempPos < _source.Length)
        {
            char c = _source[tempPos];
            if (c == ' ') { indent++; tempPos++; }
            else if (c == '\t') { indent += _rules.TabWidth; tempPos++; }
            else if (c == '\n') { indent = 0; tempPos++; }
            else if (c == '#')
            {
                while (tempPos < _source.Length && _source[tempPos] != '\n')
                {
                    tempPos++;
                }
            }
            else
            {
                break;
            }
        }

        int currentIndent = _indentStack.Peek();

        if (indent > currentIndent)
        {
            _indentStack.Push(indent);
            tokens.Add(new(TokenType.Indent, "", line, column));
        }
        else if (indent < currentIndent)
        {
            while (_indentStack.Count > 1 && _indentStack.Peek() > indent)
            {
                _indentStack.Pop();
                tokens.Add(new(TokenType.Dedent, "", line, column));
            }
            if (_indentStack.Peek() != indent)
            {
                ReportError("Inconsistent indentation", line, column);
            }
        }
    }

    private bool MatchString(string expected)
    {
        if (_position + expected.Length > _source.Length)
        {
            return false;
        }

        for (int i = 0; i < expected.Length; i++)
        {
            if (_source[_position + i] == expected[i])
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private void ReportError(string message, int line, int column)
    {
        _context?.Diagnostics.Add(new(
            _context.SourceName,
            message,
            line,
            column,
            DiagnosticSeverity.Error));
    }
}
```

---

### `Snek.Core\Lexing\LexerRules.cs`

```csharp
namespace Snek.Core.Lexing;

public class LexerRules
{
    public Dictionary<string, TokenType> Keywords { get; } = [];
    public List<(string Pattern, TokenType Type)> Operators { get; } = [];
    public char StringDelimiter { get; set; } = '"';
    public char CharDelimiter { get; set; } = '\'';
    public bool SupportsIndentation { get; set; } = true;
    public int TabWidth { get; set; } = 2;
    public bool AllowTrailingCommas { get; set; } = true;
    public HashSet<char> IdentifierStartChars { get; } = ['_'];
    public HashSet<char> IdentifierContinueChars { get; } = [];

    public LexerRules()
    {
        // Default Snek keywords
        Keywords["fn"] = TokenType.KeywordFn;
        Keywords["if"] = TokenType.KeywordIf;
        Keywords["else"] = TokenType.KeywordElse;
        Keywords["while"] = TokenType.KeywordWhile;
        Keywords["for"] = TokenType.KeywordFor;
        Keywords["in"] = TokenType.KeywordIn;
        Keywords["return"] = TokenType.KeywordReturn;
        Keywords["break"] = TokenType.KeywordBreak;
        Keywords["continue"] = TokenType.KeywordContinue;
        Keywords["pass"] = TokenType.KeywordPass;
        Keywords["import"] = TokenType.KeywordImport;
        Keywords["from"] = TokenType.KeywordFrom;
        Keywords["as"] = TokenType.KeywordAs;
        Keywords["class"] = TokenType.KeywordClass;
        Keywords["char"] = TokenType.KeywordChar;
        Keywords["string"] = TokenType.KeywordString;
        Keywords["bool"] = TokenType.KeywordBool;
        Keywords["true"] = TokenType.KeywordTrue;
        Keywords["false"] = TokenType.KeywordFalse;
        Keywords["none"] = TokenType.KeywordNone;
        Keywords["and"] = TokenType.KeywordAnd;
        Keywords["or"] = TokenType.KeywordOr;
        Keywords["not"] = TokenType.KeywordNot;

        // Default operators (longest first to avoid prefix conflicts)
        Operators.Add(("**=", TokenType.DoubleStarAssign));
        Operators.Add(("//=", TokenType.DoubleSlashAssign));
        Operators.Add(("<<=", TokenType.LeftShiftAssign));
        Operators.Add((">>=", TokenType.RightShiftAssign));
        Operators.Add(("+=", TokenType.PlusAssign));
        Operators.Add(("-=", TokenType.MinusAssign));
        Operators.Add(("*=", TokenType.StarAssign));
        Operators.Add(("/=", TokenType.SlashAssign));
        Operators.Add(("%=", TokenType.PercentAssign));
        Operators.Add(("&=", TokenType.AmpersandAssign));
        Operators.Add(("|=", TokenType.PipeAssign));
        Operators.Add(("^=", TokenType.CaretAssign));
        Operators.Add(("==", TokenType.DoubleEquals));
        Operators.Add(("!=", TokenType.NotEquals));
        Operators.Add(("<=", TokenType.LessEqual));
        Operators.Add((">=", TokenType.GreaterEqual));
        Operators.Add(("->", TokenType.Arrow));
        Operators.Add(("**", TokenType.DoubleStar));
        Operators.Add(("//", TokenType.DoubleSlash));
        Operators.Add(("<<", TokenType.LeftShift));
        Operators.Add((">>", TokenType.RightShift));
        Operators.Add(("+", TokenType.Plus));
        Operators.Add(("-", TokenType.Minus));
        Operators.Add(("*", TokenType.Star));
        Operators.Add(("/", TokenType.Slash));
        Operators.Add(("%", TokenType.Percent));
        Operators.Add(("=", TokenType.Equals));
        Operators.Add(("<", TokenType.LessThan));
        Operators.Add((">", TokenType.GreaterThan));
        Operators.Add((":", TokenType.Colon));
        Operators.Add((",", TokenType.Comma));
        Operators.Add((".", TokenType.Dot));
        Operators.Add(("(", TokenType.LeftParen));
        Operators.Add((")", TokenType.RightParen));
        Operators.Add(("[", TokenType.LeftBracket));
        Operators.Add(("]", TokenType.RightBracket));
        Operators.Add(("{", TokenType.LeftBrace));
        Operators.Add(("}", TokenType.RightBrace));
        Operators.Add(("@", TokenType.At));
        Operators.Add(("?", TokenType.Question));
        Operators.Add(("&", TokenType.Ampersand));
        Operators.Add(("|", TokenType.Pipe));
        Operators.Add(("~", TokenType.Tilde));
        Operators.Add(("^", TokenType.Caret));
    }

    public static LexerRules CreatePythonStyle()
    {
        LexerRules rules = new();
        rules.Keywords["def"] = TokenType.KeywordDef;
        return rules;
    }
}
```

---

### `Snek.Core\Lexing\Token.cs`

```csharp
namespace Snek.Core.Lexing;

public record Token(TokenType Type, string Value, int Line, int Column)
{
    public override string ToString()
    {
        return $"[{Line}:{Column}] {Type}='{Value}'";
    }
}
```

---

### `Snek.Core\Lexing\TokenType.cs`

```csharp
namespace Snek.Core.Lexing;

public enum TokenType
{
    // Structural
    Unknown,
    Eof,
    Newline,
    Indent,
    Dedent,

    // Keywords
    KeywordFn,
    KeywordIf,
    KeywordElse,
    KeywordWhile,
    KeywordFor,
    KeywordIn,
    KeywordReturn,
    KeywordBreak,
    KeywordContinue,
    KeywordPass,
    KeywordImport,
    KeywordFrom,
    KeywordAs,
    KeywordClass,
    KeywordDef,  // Alias for fn in alternate syntax
    KeywordChar,
    KeywordString,
    KeywordBool,
    KeywordTrue,
    KeywordFalse,
    KeywordNone,
    KeywordAnd,
    KeywordOr,
    KeywordNot,

    // Literals
    Identifier,
    IntegerLiteral,
    FloatLiteral,
    StringLiteral,
    CharLiteral,

    // Operators
    Plus,
    Minus,
    Star,
    Slash,
    Percent,
    DoubleStar,     // **
    DoubleSlash,    // //
    Equals,         // =
    DoubleEquals,   // ==
    NotEquals,      // !=
    LessThan,
    GreaterThan,
    LessEqual,
    GreaterEqual,
    Arrow,          // ->
    Colon,
    Comma,
    Dot,
    LeftParen,
    RightParen,
    LeftBracket,
    RightBracket,
    LeftBrace,
    RightBrace,
    At,             // @ for decorators
    Question,       // ? for optional
    Ampersand,
    Pipe,
    Tilde,
    Caret,
    LeftShift,
    RightShift,

    // Assignment operators
    PlusAssign,
    MinusAssign,
    StarAssign,
    SlashAssign,
    PercentAssign,
    DoubleStarAssign,
    DoubleSlashAssign,
    AmpersandAssign,
    PipeAssign,
    CaretAssign,
    LeftShiftAssign,
    RightShiftAssign,
}
```

---

### `Snek.Core\Parsing\ExpressionParser.cs`

```csharp
using Snek.Core.Ast;
using Snek.Core.Lexing;

namespace Snek.Core.Parsing;

public class ExpressionParser
{
    private readonly ParserStream _stream;

    public ExpressionParser(ParserStream stream)
    {
        _stream = stream;
    }

    public ExpressionNode ParseExpression(int precedence = 0)
    {
        ExpressionNode left = ParsePrimary();

        while (true)
        {
            Token op = _stream.Current;
            int nextPrecedence = GetPrecedence(op.Type);

            if (nextPrecedence < precedence)
            {
                break;
            }

            _stream.Advance();

            ExpressionNode right = ParseExpression(nextPrecedence + 1);
            left = new BinaryExpressionNode(left, op, right);
        }

        return left;
    }

    private ExpressionNode ParsePrimary()
    {
        if (_stream.Match(TokenType.Identifier))
        {
            Token name = _stream.Previous;

            if (_stream.Match(TokenType.LeftParen))
            {
                return ParseCall(name);
            }
            else
            {
                if (_stream.Match(TokenType.Dot))
                {
                    return ParseMemberAccess(name);
                }
                else
                {
                    if (_stream.Match(TokenType.LeftBracket))
                    {
                        return ParseIndex(name);
                    }
                    else
                    {
                        return new IdentifierExpressionNode(name);
                    }
                }
            }
        }

        if (_stream.Match(TokenType.StringLiteral) ||
            _stream.Match(TokenType.IntegerLiteral) ||
            _stream.Match(TokenType.FloatLiteral))
        {
            return new LiteralExpressionNode(_stream.Previous);
        }

        if (_stream.Match(TokenType.KeywordTrue) ||
            _stream.Match(TokenType.KeywordFalse) ||
            _stream.Match(TokenType.KeywordNone))
        {
            return new LiteralExpressionNode(_stream.Previous);
        }

        if (_stream.Match(TokenType.LeftParen))
        {
            ExpressionNode expr = ParseExpression();
            _stream.Consume(TokenType.RightParen);
            return expr;
        }

        if (_stream.Match(TokenType.Minus) || _stream.Match(TokenType.KeywordNot))
        {
            Token op = _stream.Previous;
            ExpressionNode operand = ParsePrimary();
            return new UnaryExpressionNode(op, operand);
        }

        if (_stream.Match(TokenType.LeftBracket))
        {
            return ParseListLiteral();
        }

        _stream.ReportError(
            $"Unexpected token '{_stream.Current.Value}' in expression",
            _stream.Current);

        _stream.Advance();

        return new LiteralExpressionNode(new(TokenType.Unknown, "unknown", -1, -1));
    }

    private CallExpressionNode ParseCall(Token callee)
    {
        List<ExpressionNode> args = [];

        if (!_stream.Match(TokenType.RightParen))
        {
            do
            {
                args.Add(ParseExpression());
            } while (_stream.Match(TokenType.Comma));

            _stream.Consume(TokenType.RightParen);
        }

        return new(new IdentifierExpressionNode(callee), args);
    }

    private MemberAccessExpressionNode ParseMemberAccess(Token obj)
    {
        Token member = _stream.Consume(TokenType.Identifier);
        return new MemberAccessExpressionNode(new IdentifierExpressionNode(obj), member);
    }

    private IndexExpressionNode ParseIndex(Token target)
    {
        ExpressionNode index = ParseExpression();
        _stream.Consume(TokenType.RightBracket);
        return new(new IdentifierExpressionNode(target), index);
    }

    private ListExpressionNode ParseListLiteral()
    {
        List<ExpressionNode> elements = [];

        if (!_stream.Match(TokenType.RightBracket))
        {
            do
            {
                elements.Add(ParseExpression());
            } while (_stream.Match(TokenType.Comma));
            _stream.Consume(TokenType.RightBracket);
        }

        return new(elements);
    }

    private static int GetPrecedence(TokenType type)
    {
        return type switch
        {
            TokenType.KeywordOr => 1,
            TokenType.KeywordAnd => 2,
            TokenType.DoubleEquals
                or TokenType.NotEquals
                or TokenType.LessThan
                or TokenType.GreaterThan
                or TokenType.LessEqual
                or TokenType.GreaterEqual => 3,
            TokenType.Plus or TokenType.Minus => 4,
            TokenType.Star or TokenType.Slash or TokenType.Percent => 5,
            TokenType.DoubleStar => 6,
            _ => -1
        };
    }
}
```

---

### `Snek.Core\Parsing\IParser.cs`

```csharp
using Snek.Core.Ast;
using Snek.Core.Lexing;
using Snek.Core.Pipeline;

namespace Snek.Core.Parsing;

public interface IParser
{
    AstNode Parse(IEnumerable<Token> tokens, CompilationContext context);
}
```

---

### `Snek.Core\Parsing\Parser.cs`

```csharp
using Snek.Core.Ast;
using Snek.Core.Lexing;
using Snek.Core.Pipeline;

namespace Snek.Core.Parsing;

public class Parser : IParser
{
    private readonly LexerRules _rules;

    public Parser(LexerRules? rules = null)
    {
        _rules = rules ?? new();
    }

    public AstNode Parse(IEnumerable<Token> tokens, CompilationContext context)
    {
        ParserStream stream = new(tokens, context);
        ExpressionParser expressionParser = new(stream);
        StatementParser statementParser = new(stream, expressionParser, _rules);

        return statementParser.ParseProgram();
    }
}
```

---

### `Snek.Core\Parsing\ParserExtensions.cs`

```csharp
using Snek.Core.Lexing;
using Snek.Core.Pipeline;

namespace Snek.Core.Parsing;

public static class ParserExtensions
{
    public static List<T> ParseCommaSeparated<T>(
        this IEnumerator<Token> tokens,
        TokenType terminator,
        Func<Token, CompilationContext, T> parseItem,
        CompilationContext context)
    {
        List<T> items = [];

        if (tokens.Current?.Type == terminator)
        {
            return items;
        }

        do
        {
            if (tokens.Current is null)
            {
                break;
            }

            items.Add(parseItem(tokens.Current, context));
            tokens.MoveNext();
        } while (tokens.Current?.Type == TokenType.Comma);

        return items;
    }

    public static void SkipToSyncPoint(
        this IEnumerator<Token> tokens,
        params TokenType[] syncPoints)
    {
        while (tokens.Current?.Type is not (TokenType.Eof or TokenType.Newline or TokenType.Dedent)
               && !syncPoints.Contains(tokens.Current.Type))
        {
            tokens.MoveNext();
        }
    }

    public static Token? Peek(this IEnumerator<Token> tokens, int offset = 0)
    {
        _ = tokens.Current;

        for (int i = 0; i <= offset && tokens.MoveNext(); i++)
        {
            if (i != offset)
            {
                continue;
            }

            return tokens.Current;
        }

        return null;
    }
}
```

---

### `Snek.Core\Parsing\ParserStream.cs`

```csharp
using Snek.Core.Lexing;
using Snek.Core.Pipeline;

namespace Snek.Core.Parsing;

public class ParserStream
{
    private readonly List<Token> _tokens;
    private readonly CompilationContext _context;
    private int _position;

    public Token Current { get; private set; }
    public Token Previous { get; private set; }

    public ParserStream(IEnumerable<Token> tokens, CompilationContext context)
    {
        _tokens = [.. tokens];
        _context = context;
        _position = 0;

        Current = _tokens.Count > 0
            ? _tokens[0]
            : new(TokenType.Eof, " ", -1, -1);

        Previous = Current;
    }

    public void Advance()
    {
        Previous = Current;
        _position++;

        Current = _position < _tokens.Count
            ? _tokens[_position]
            : new(TokenType.Eof, " ", -1, -1);
    }

    public Token Peek(int offset = 1)
    {
        int index = _position + offset;

        if (index >= 0 && index < _tokens.Count)
        {
            return _tokens[index];
        }

        return new(TokenType.Eof, "", -1, -1);
    }

    public bool Match(TokenType type)
    {
        if (Current.Type != type)
        {
            return false;
        }

        Advance();
        return true;
    }

    public Token Consume(TokenType type)
    {
        if (Current.Type != type)
        {
            ReportError($"Expected '{type}' but got '{Current.Value}'", Current);
            return Current;
        }

        Token token = Current;
        Advance();
        return token;
    }

    public void ReportError(string message, Token atToken)
    {
        _context.Diagnostics.Add(new(
            _context.SourceName,
            message,
            atToken.Line,
            atToken.Column,
            DiagnosticSeverity.Error,
            atToken.Value.Length));
    }
}
```

---

### `Snek.Core\Parsing\StatementParser.cs`

```csharp
using Snek.Core.Ast;
using Snek.Core.Lexing;

namespace Snek.Core.Parsing;

public class StatementParser
{
    private readonly ParserStream _stream;
    private readonly ExpressionParser _expressions;
    private readonly LexerRules _rules;
    private int _expectedIndent;

    public StatementParser(ParserStream stream, ExpressionParser expressions, LexerRules rules)
    {
        _stream = stream;
        _expressions = expressions;
        _rules = rules;
        _expectedIndent = 0;
    }

    public ProgramNode ParseProgram()
    {
        List<StatementNode> statements = [];

        while (!_stream.Match(TokenType.Eof))
        {
            if (_stream.Match(TokenType.Newline))
            {
                continue;
            }

            // Handle top-level indentation adjustments (if any)
            if (_stream.Match(TokenType.Dedent))
            {
                _expectedIndent -= _rules.TabWidth;
                continue;
            }

            if (_stream.Match(TokenType.Indent))
            {
                _expectedIndent += _rules.TabWidth;
                continue;
            }

            StatementNode? stmt = ParseStatement();

            if (stmt == null)
            {
                continue;
            }

            statements.Add(stmt);
        }

        return new(statements);
    }

    private StatementNode? ParseStatement()
    {
        if (_stream.Match(TokenType.KeywordFn) || _stream.Match(TokenType.KeywordDef))
        {
            return ParseFunctionDef();
        }

        if (_stream.Match(TokenType.KeywordIf))
        {
            return ParseIfStatement();
        }

        if (_stream.Match(TokenType.KeywordWhile))
        {
            return ParseWhileStatement();
        }

        if (_stream.Match(TokenType.KeywordReturn))
        {
            return ParseReturnStatement();
        }

        if (_stream.Match(TokenType.KeywordPass))
        {
            ExpectNewline();
            return new PassStatementNode();
        }

        if (_stream.Match(TokenType.KeywordBreak))
        {
            ExpectNewline();
            return new BreakStatementNode();
        }

        if (_stream.Match(TokenType.KeywordContinue))
        {
            ExpectNewline();
            return new ContinueStatementNode();
        }

        // Check for variable declaration: identifier ':' type ('=' expression)?
        if (_stream.Current.Type == TokenType.Identifier && _stream.Peek().Type == TokenType.Colon)
        {
            return ParseVariableDeclaration();
        }

        ExpressionNode expr = _expressions.ParseExpression();
        ExpectNewline();
        return new ExpressionStatementNode(expr);
    }

    private FunctionDefNode ParseFunctionDef()
    {
        Token name = _stream.Consume(TokenType.Identifier);
        _stream.Consume(TokenType.LeftParen);
        List<ParameterNode> parameters = ParseParameters();

        TypeNode? returnType = null;

        if (_stream.Match(TokenType.Arrow))
        {
            returnType = ParseTypeAnnotation();
        }

        _stream.Consume(TokenType.Colon);
        ExpectNewline();

        int bodyIndent = _expectedIndent + _rules.TabWidth;
        List<StatementNode> body = ParseIndentedBlock();

        return new(name, parameters, returnType, body, bodyIndent);
    }

    private VariableDeclarationNode ParseVariableDeclaration()
    {
        Token name = _stream.Consume(TokenType.Identifier);
        _stream.Consume(TokenType.Colon);
        TypeNode type = ParseTypeAnnotation();

        ExpressionNode? initializer = null;

        if (_stream.Match(TokenType.Equals))
        {
            initializer = _expressions.ParseExpression();
        }

        ExpectNewline();
        return new VariableDeclarationNode(name, type, initializer, _expectedIndent);
    }

    private List<ParameterNode> ParseParameters()
    {
        List<ParameterNode> parameters = [];

        if (_stream.Match(TokenType.RightParen))
        {
            return parameters;
        }

        do
        {
            Token paramName = _stream.Consume(TokenType.Identifier);

            TypeNode? typeAnn = null;

            if (_stream.Match(TokenType.Colon))
            {
                typeAnn = ParseTypeAnnotation();
            }

            ExpressionNode? defaultValue = null;

            if (_stream.Match(TokenType.Equals))
            {
                defaultValue = _expressions.ParseExpression();
            }

            parameters.Add(new(paramName, typeAnn, defaultValue));
        } while (_stream.Match(TokenType.Comma));

        _stream.Consume(TokenType.RightParen);
        return parameters;
    }

    private TypeNode ParseTypeAnnotation()
    {
        Token nameToken;

        if (_stream.Match(TokenType.Identifier))
        {
            nameToken = _stream.Previous;
        }
        else
        {
            if (_stream.Match(TokenType.KeywordChar) ||
                 _stream.Match(TokenType.KeywordString) || _stream.Match(TokenType.KeywordBool))
            {
                nameToken = _stream.Previous;
            }
            else
            {
                nameToken = _stream.Consume(TokenType.Identifier);
            }
        }

        if (!_stream.Match(TokenType.LessThan))
        {
            return TypeNode.Simple(nameToken);
        }

        List<TypeNode> args = [];

        do
        {
            args.Add(ParseTypeAnnotation());
        }
        while (_stream.Match(TokenType.Comma));

        _stream.Consume(TokenType.GreaterThan);
        return TypeNode.Generic(nameToken, args);
    }

    private IfStatementNode ParseIfStatement()
    {
        ExpressionNode condition = _expressions.ParseExpression();
        _stream.Consume(TokenType.Colon);
        ExpectNewline();

        int thenIndent = _expectedIndent + _rules.TabWidth;
        List<StatementNode> thenBody = ParseIndentedBlock();

        List<StatementNode>? elseBody = null;

        if (!_stream.Match(TokenType.KeywordElse))
        {
            return new(condition, thenBody, elseBody, thenIndent);
        }

        _stream.Consume(TokenType.Colon);
        ExpectNewline();

        _ = _expectedIndent + _rules.TabWidth;

        elseBody = ParseIndentedBlock();

        return new(condition, thenBody, elseBody, thenIndent);
    }

    private WhileStatementNode ParseWhileStatement()
    {
        ExpressionNode condition = _expressions.ParseExpression();
        _stream.Consume(TokenType.Colon);
        ExpectNewline();

        int bodyIndent = _expectedIndent + _rules.TabWidth;
        List<StatementNode> body = ParseIndentedBlock();

        return new WhileStatementNode(condition, body, bodyIndent);
    }

    private ReturnStatementNode ParseReturnStatement()
    {
        ExpressionNode? value = null;

        if (_stream.Current.Type is not (TokenType.Newline or TokenType.Eof or TokenType.Dedent))
        {
            value = _expressions.ParseExpression();
        }

        ExpectNewline();
        return new ReturnStatementNode(value);
    }

    private List<StatementNode> ParseIndentedBlock()
    {
        if (!_stream.Match(TokenType.Indent))
        {
            _stream.ReportError(
                $"Expected indented block but got '{_stream.Current.Value}'",
                _stream.Current);

            SyncToBlockEnd();
            return [];
        }

        List<StatementNode> statements = [];

        while (!_stream.Match(TokenType.Dedent) && !_stream.Match(TokenType.Eof))
        {
            if (_stream.Match(TokenType.Newline))
            {
                continue;
            }

            StatementNode? stmt = ParseStatement();

            if (stmt == null)
            {
                continue;
            }

            statements.Add(stmt);
        }
        return statements;
    }

    private void SyncToBlockEnd()
    {
        while (_stream.Current.Type is not TokenType.Dedent and
               not TokenType.Eof)
        {
            _stream.Advance();
        }

        _stream.Match(TokenType.Dedent);
    }

    private void ExpectNewline()
    {
        if (_stream.Match(TokenType.Newline) || _stream.Match(TokenType.Eof))
        {
            return;
        }

        // Inside an indented block, Dedent follows the last statement
        if (_stream.Current.Type == TokenType.Dedent)
        {
            return;
        }

        _stream.ReportError(
            $"Expected newline after ':' but got '{_stream.Current.Value}'",
            _stream.Current);

        // Skip to next line to prevent cascading errors
        SyncToNewline();
    }

    private void SyncToNewline()
    {
        while (_stream.Current.Type is not TokenType.Newline and
               not TokenType.Dedent and
               not TokenType.Eof)
        {
            _stream.Advance();
        }

        _stream.Match(TokenType.Newline);
    }
}
```

---

### `Snek.Core\Pipeline\CompilationContext.cs`

```csharp
using Snek.Core.Diagnoistics;

namespace Snek.Core.Pipeline;

public class CompilationContext
{
    public string SourceName { get; }
    public PipelineOptions Options { get; }
    public List<Diagnostic> Diagnostics { get; } = [];
    public Dictionary<string, object?> StageData { get; } = [];

    public CompilationContext(string sourceName, PipelineOptions options)
    {
        SourceName = sourceName;
        Options = options;
    }

    public T? GetStageData<T>(string key) where T : class
    {
        return StageData.TryGetValue(key, out object? value)
            ? value as T
            : null;
    }

    public void SetStageData<T>(string key, T value) where T : class
    {
        StageData[key] = value;
    }
}
```

---

### `Snek.Core\Pipeline\CompilationResult.cs`

```csharp
namespace Snek.Core.Pipeline;

public record CompilationResult(string? Output, IReadOnlyList<Diagnostic> Diagnostics)
{
    public CompilationResult(IReadOnlyList<Diagnostic> diagnostics) : this(null, diagnostics)
    {
    }

    public bool Success => !Diagnostics.Any(d => d.IsError);
}
```

---

### `Snek.Core\Pipeline\CompilerPipeline.cs`

```csharp
using Snek.Core.Analysis;
using Snek.Core.Ast;
using Snek.Core.Diagnoistics;
using Snek.Core.Generation;
using Snek.Core.Lexing;
using Snek.Core.Parsing;

namespace Snek.Core.Pipeline;

public class CompilerPipeline
{
    private readonly ILexer _lexer;
    private readonly IParser _parser;
    private readonly ISemanticAnalyzer _analyzer;
    private readonly ICodeGenerator _generator;
    private readonly PipelineOptions _options;

    public CompilerPipeline(
        ILexer lexer,
        IParser parser,
        ISemanticAnalyzer analyzer,
        ICodeGenerator generator,
        PipelineOptions? options = null)
    {
        _lexer = lexer ?? throw new ArgumentNullException(nameof(lexer));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _options = options ?? new PipelineOptions();
    }

    public CompilationResult Compile(string source, string sourceName = "<input>")
    {
        CompilationContext context = new(sourceName, _options);

        try
        {
            // Stage 1: Lexing
            if (_options.EnableLogging)
            {
                Console.WriteLine($"[{sourceName}] Lexing...");
            }

            IEnumerable<Token> tokens = _lexer.Tokenize(source, context);

            // Continue to parsing even with lexer errors — the parser
            // can often report better, more context-aware diagnostics.
            // Stage 2: Parsing
            if (_options.EnableLogging)
            {
                Console.WriteLine($"[{sourceName}] Parsing...");
            }

            AstNode ast = _parser.Parse(tokens, context);
            if (context.Diagnostics.Any(d => d.IsError))
            {
                return new CompilationResult(context.Diagnostics);
            }

            // Stage 3: Semantic Analysis
            if (_options.EnableLogging)
            {
                Console.WriteLine($"[{sourceName}] Analyzing...");
            }

            _analyzer.Analyze(ast, context);
            if (context.Diagnostics.Any(d => d.IsError))
            {
                return new CompilationResult(context.Diagnostics);
            }

            // Stage 4: Code Generation
            if (_options.EnableLogging)
            {
                Console.WriteLine($"[{sourceName}] Generating...");
            }

            string? output = _generator.Generate(ast, context);

            return context.Diagnostics.Any(d => d.IsError)
                ? new(context.Diagnostics)
                : new(output, context.Diagnostics);
        }
        catch (Exception ex)
        {
            context.Diagnostics.Add(new(
                sourceName,
                $"Internal compiler error: {ex.Message}",
                -1,
                -1,
                DiagnosticSeverity.Error));

            return new(context.Diagnostics);
        }
    }
}
```

---

### `Snek.Core\Pipeline\IPipelineStage.cs`

```csharp
namespace Snek.Core.Pipeline;

public interface IPipelineStage
{
    bool Execute(CompilationContext context);

    IReadOnlyList<Diagnostic> GetDiagnostics();
}
```

---

### `Snek.Core\Pipeline\PipelineOptions.cs`

```csharp
namespace Snek.Core.Pipeline;

public record PipelineOptions
{
    public bool EnableLogging { get; set; } = false;
    public bool EnableOptimizations { get; set; } = false;
    public TargetPlatform Target { get; set; } = TargetPlatform.X86;
}
```

---

### `Snek.Core\Pipeline\TargetPlatform.cs`

```csharp
namespace Snek.Core.Pipeline;

public enum TargetPlatform
{
    X86,
    X64,
    WebAssembly
}
```

---

### `Snek\CompileCommand.cs`

```csharp
using Snek.Core.Compiler;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Snek;

[Description("Snek Compiler - compiles .snek files to executables")]
public class CompileCommand : Command<CompilerSettings>
{
    protected override int Execute(
        [NotNull] CommandContext context,
        [NotNull] CompilerSettings settings,
        CancellationToken cancellationToken)
    {
        CompilerOptions options = new()
        {
            OutputPath = settings.OutputPath,
            Syntax = settings.Syntax,
            Verbose = settings.Verbose,
            AsmOnly = settings.AsmOnly
        };

        CompilerService compiler = new(options);
        (bool success, _, _) = compiler.Compile(settings.InputPath);

        return success ? 0 : 1;
    }
}
```

---

### `Snek\CompilerSettings.cs`

```csharp
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Snek;

public class CompilerSettings : CommandSettings
{
    [CommandArgument(0, "<INPUT>")]
    [Description("Path to the input .snek file")]
    public required string InputPath { get; set; }

    [CommandOption("-o|--output <OUTPUT>")]
    [Description("Specify output file (default: output.asm or output.exe)")]
    public string? OutputPath { get; set; }

    [CommandOption("--syntax <SYNTAX>")]
    [Description("Use alternate syntax: python, cstyle (default: python)")]
    [DefaultValue("python")]
    public string Syntax { get; set; } = "python";

    [CommandOption("-v|--verbose")]
    [Description("Enable detailed logging")]
    public bool Verbose { get; set; }

    [CommandOption("--asm-only")]
    [Description("Stop after generating assembly (do not assemble)")]
    public bool AsmOnly { get; set; }
}
```

---

### `Snek\Program.cs`

```csharp
using Spectre.Console.Cli;

namespace Snek;

public class Program
{
    public static int Main(string[] args)
    {
        CommandApp<CompileCommand> app = new();
        app.Configure(config =>
        {
            config.SetApplicationName("snek");
            config.SetApplicationVersion("1.0.0");
        });

        return app.Run(args);
    }
}
```

---

### `Snek\Snek.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<OutputType>Exe</OutputType>
		<TargetFramework>net10.0</TargetFramework>
		<ImplicitUsings>enable</ImplicitUsings>
		<Nullable>enable</Nullable>
	</PropertyGroup>

	<ItemGroup>
		<PackageReference Include="Spectre.Console.Cli" Version="0.55.0" />
	</ItemGroup>

	<ItemGroup>
		<ProjectReference Include="..\Snek.Core\Snek.Core.csproj" />
	</ItemGroup>

</Project>
```

---

### `Snek.Tests\Snek.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<TargetFramework>net10.0</TargetFramework>
		<IsPackable>false</IsPackable>
		<ImplicitUsings>enable</ImplicitUsings>
		<Nullable>enable</Nullable>
	</PropertyGroup>

	<ItemGroup>
		<PackageReference Include="coverlet.collector" Version="10.0.1">
		  <PrivateAssets>all</PrivateAssets>
		  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
		</PackageReference>
		<PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.7.0" />
		<PackageReference Include="xunit.runner.visualstudio" Version="3.1.5">
		  <PrivateAssets>all</PrivateAssets>
		  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
		</PackageReference>
		<PackageReference Include="FluentAssertions" Version="8.10.0" />
		<PackageReference Include="xunit.v3" Version="3.2.2" />
	</ItemGroup>

	<ItemGroup>
		<ProjectReference Include="..\Snek.Core\Snek.Core.csproj" />
		<ProjectReference Include="..\Snek\Snek.csproj" />
	</ItemGroup>

	<ItemGroup>
		<Using Include="Xunit" />
	</ItemGroup>

</Project>
```

---

### `Snek.Tests\TestHelpers.cs`

```csharp
using Snek.Core.Ast;
using Snek.Core.Lexing;

namespace Snek.Tests;

public static class TestHelpers
{
    public static Token CreateToken(TokenType type, string value, int line = 1, int column = 1)
    {
        return new Token(type, value, line, column);
    }

    public static LiteralExpressionNode CreateIntLiteral(int value)
    {
        return new LiteralExpressionNode(CreateToken(TokenType.IntegerLiteral, value.ToString()));
    }

    public static LiteralExpressionNode CreateStringLiteral(string value)
    {
        return new LiteralExpressionNode(CreateToken(TokenType.StringLiteral, value));
    }

    public static IdentifierExpressionNode CreateIdentifier(string name)
    {
        return new IdentifierExpressionNode(CreateToken(TokenType.Identifier, name));
    }

    public static BinaryExpressionNode CreateBinary(ExpressionNode left, TokenType op, ExpressionNode right)
    {
        return new BinaryExpressionNode(left, CreateToken(op, GetOperatorString(op)), right);
    }

    private static string GetOperatorString(TokenType op)
    {
        return op switch
        {
            TokenType.Plus => "+",
            TokenType.Minus => "-",
            TokenType.Star => "*",
            TokenType.Slash => "/",
            TokenType.DoubleEquals => "==",
            TokenType.NotEquals => "!=",
            TokenType.LessThan => "<",
            TokenType.GreaterThan => ">",
            TokenType.LessEqual => "<=",
            TokenType.GreaterEqual => ">=",
            _ => op.ToString()
        };
    }
}
```

---

### `Snek.Tests\Analysis\SemanticAnalyzerTests.cs`

```csharp
using FluentAssertions;
using Snek.Core.Analysis;
using Snek.Core.Lexing;
using Snek.Core.Parsing;
using Snek.Core.Pipeline;

namespace Snek.Tests.Analysis;

public class SemanticAnalyzerTests
{
    private readonly SemanticAnalyzer _analyzer;
    private readonly CompilationContext _context;

    public SemanticAnalyzerTests()
    {
        _analyzer = new();
        _context = new("test.snek", new PipelineOptions());
    }

    private void AnalyzeSource(string source)
    {
        Lexer lexer = new();
        Parser parser = new();
        IEnumerable<Token> tokens = lexer.Tokenize(source, _context);
        AstNode ast = parser.Parse(tokens, _context);
        _analyzer.Analyze(ast, _context);
    }

    [Fact]
    public void Analyze_UndefinedIdentifier_ReportsError()
    {
        string source = """
            fn test():
              return undefinedVar

            """;
        AnalyzeSource(source);

        _context.Diagnostics.Should().Contain(d => d.IsError && d.Message.Contains("Undefined identifier"));
    }

    [Fact]
    public void Analyze_TypeMismatch_ReturnsError()
    {
        string source = """
            fn foo() -> i32:
              return "wrong"

            """;
        AnalyzeSource(source);

        _context.Diagnostics.Should().Contain(d => d.IsError && d.Message.Contains("Return type mismatch"));
    }

    [Fact]
    public void Analyze_NonVoidFunctionWithoutReturn_ReportsError()
    {
        string source = """
            fn foo() -> i32:
              pass

            """;
        AnalyzeSource(source);

        _context.Diagnostics.Should().ContainSingle(d =>
            d.IsError && d.Message.Contains("must return a value"));
    }

    [Fact]
    public void Analyze_IfConditionNotBool_ReportsError()
    {
        string source = """
            fn test():
              if "string":
                pass

            """;
        AnalyzeSource(source);

        _context.Diagnostics.Should().Contain(d => d.IsError && d.Message.Contains("Condition must be bool"));
    }

    [Fact]
    public void Analyze_WhileConditionNotBool_ReportsError()
    {
        string source = """
            fn test():
              while 42:
                pass

            """;
        AnalyzeSource(source);

        _context.Diagnostics.Should().Contain(d => d.IsError && d.Message.Contains("While condition must be bool"));
    }

    [Fact]
    public void Analyze_FunctionCallWithWrongArity_ReportsError()
    {
        string source = """
            fn foo(x: int):
              pass

            fn test():
              return foo()

            """;
        AnalyzeSource(source);

        // Since foo is called with wrong arity (0 args instead of 1)
        // The error should mention arity mismatch
        _context.Diagnostics.Should().ContainSingle(d =>
            d.IsError && d.Message.Contains("expects 1 args, got 0"));
    }

    [Fact]
    public void Analyze_ValidFunctionCall_ResolvesReturnType()
    {
        string source = """
            fn foo() -> i32:
              return 42

            fn test() -> i32:
              return foo()

            """;

        AnalyzeSource(source);

        // Resolve the type of a call to foo(), which should be i32
        CallExpressionNode callExpr = new(
            new IdentifierExpressionNode(new(TokenType.Identifier, "foo", 1, 1)),
            []);

        TypeKind? type = _analyzer.ResolveType(callExpr, _context);

        type.Should().Be(TypeKind.I32);
        _context.Diagnostics.Should().NotContain(d => d.IsError);
    }

    [Fact]
    public void Analyze_BinaryExpression_PromotesTypes()
    {
        string source = """
            fn test() -> f64:
              return 1 + 2.5

            """;

        AnalyzeSource(source);

        _context.Diagnostics.Should().NotContain(d => d.IsError);
    }

    [Fact]
    public void Analyze_ComparisonExpression_ReturnsBool()
    {
        string source = """
            fn test() -> bool:
              return 5 > 3

            """;

        AnalyzeSource(source);

        _context.Diagnostics.Should().NotContain(d => d.IsError);
    }
}
```

---

### `Snek.Tests\Generation\CodeGeneratorTests.cs`

```csharp
using FluentAssertions;
using Snek.Core.Analysis;
using Snek.Core.Ast;
using Snek.Core.Generation;
using Snek.Core.Lexing;
using Snek.Core.Parsing;
using Snek.Core.Pipeline;

namespace Snek.Tests.Generation;

public sealed class CodeGeneratorTests
{
    private readonly CodeGenerator _generator;
    private readonly CompilationContext _context;

    public CodeGeneratorTests()
    {
        _generator = new();
        _context = new("test.snek", new());
    }

    private string GenerateSource(string source)
    {
        Lexer lexer = new();
        Parser parser = new();
        SemanticAnalyzer analyzer = new();

        IEnumerable<Token> tokens = lexer.Tokenize(source, _context);
        AstNode ast = parser.Parse(tokens, _context);
        analyzer.Analyze(ast, _context);

        return _generator.Generate(ast, _context) ?? string.Empty;
    }

    [Fact]
    public void Generate_EmptyProgram_ProducesValidHeader()
    {
        string source = "pass";

        string output = GenerateSource(source);

        output.Should().Contain("format PE console");
        output.Should().Contain("entry start");
        output.Should().Contain("section '.text' code readable executable");
    }

    [Fact]
    public void Generate_StringLiteral_EmitsDataSection()
    {
        string source = "print(\"hello\")";
        string output = GenerateSource(source);

        output.Should().Contain("section '.data'");
        output.Should().Contain("hello");
    }

    [Fact]
    public void Generate_FunctionCall_EmitsCallInstruction()
    {
        string source = "print(\"test\")";
        string output = GenerateSource(source);

        output.Should().Contain("call [printf]");
    }

    [Fact]
    public void Generate_IntegerLiteral_PushesValue()
    {
        string source = "42";
        string output = GenerateSource(source);

        output.Should().Contain("push 42");
    }

    [Fact]
    public void Generate_BinaryAddition_EmitsAddInstruction()
    {
        string source = "1 + 2";
        string output = GenerateSource(source);

        output.Should().Contain("add eax, ebx");
    }

    [Fact]
    public void Generate_IfStatement_EmitsConditionalJump()
    {
        string source = """
            if true:
              x = 1
            """;

        string output = GenerateSource(source);

        output.Should().Contain("jz");
        output.Should().Contain("_else_");
        output.Should().Contain("_endif_");
    }

    [Fact]
    public void Generate_WhileLoop_EmitsLoopStructure()
    {
        string source = """
            while x < 10:
              x = x + 1
            """;

        string output = GenerateSource(source);

        output.Should().Contain("_while_");
        output.Should().Contain("_endwhile_");
        output.Should().Contain("jmp");
    }

    [Fact]
    public void Generate_ReturnStatement_EmitsReturnSequence()
    {
        string source = """
            fn foo() -> int:
              return 42
            """;

        string output = GenerateSource(source);

        output.Should().Contain("leave");
        output.Should().Contain("ret");
    }

    [Fact]
    public void Generate_ExternalFunction_DeclaredInImportSection()
    {
        string source = "customFunc()";

        string output = GenerateSource(source);

        output.Should().Contain("section '.idata'");
        output.Should().Contain("customFunc");
    }

    [Fact]
    public void DeclareAndUseVariable_ShouldStoreAndLoadValue()
    {
        string source = """
            x: i32 = 42
            print(x)
            """;

        string output = GenerateSource(source);

        output.Should().Contain("mov [ebp-4], eax");  // Store x
        output.Should().Contain("mov eax, [ebp-4]");  // Load x
        output.Should().Contain("push eax");          // Pass to print
    }

    [Fact]
    public void StringVariable_ShouldStoreString()
    {
        string source = """
            msg: str = "Hello"
            print(msg)
            """;

        string output = GenerateSource(source);

        output.Should().Contain("section '.data'");
        output.Should().Contain("Hello");
        output.Should().Contain("mov [ebp-4], eax");
    }

    [Fact]
    public void MultipleVariables_ShouldGetDifferentOffsets()
    {
        string source = """
            a: i32 = 1
            b: i32 = 2
            c: i32 = a + b
            """;

        string output = GenerateSource(source);

        output.Should().Contain("[ebp-4]");  // a
        output.Should().Contain("[ebp-8]");  // b
        output.Should().Contain("[ebp-12]"); // c
    }

    [Fact]
    public void Generator_VariableWithoutInitializer_ShouldDefaultToZero()
    {
        string source = "x: i32";

        string output = GenerateSource(source);

        output.Should().Contain("xor eax, eax");
        output.Should().Contain("mov [ebp-4], eax");
    }

    [Fact]
    public void Generate_TypeMismatch_ShouldReportError()
    {
        string source = "x: i32 = \"hello\"";
        Lexer lexer = new();
        Parser parser = new();
        SemanticAnalyzer analyzer = new();
        CompilationContext context = new("test.snek", new());

        IEnumerable<Token> tokens = lexer.Tokenize(source, context);
        AstNode ast = parser.Parse(tokens, context);
        analyzer.Analyze(ast, context);

        context.Diagnostics.Should().Contain(d => d.Message.Contains("Type mismatch"));
    }

    [Fact]
    public void Generate_UndefinedVariable_ShouldReportError()
    {
        string source = "print(undefinedVar)";
        Lexer lexer = new();
        Parser parser = new();
        SemanticAnalyzer analyzer = new();
        CompilationContext context = new("test.snek", new());

        IEnumerable<Token> tokens = lexer.Tokenize(source, context);
        AstNode ast = parser.Parse(tokens, context);
        analyzer.Analyze(ast, context);

        context.Diagnostics.Should().Contain(d => d.Message.Contains("Undefined identifier"));
    }
}
```

---

### `Snek.Tests\Lexing\LexerTests.cs`

```csharp
using FluentAssertions;
using Snek.Core.Lexing;
using Snek.Core.Pipeline;

namespace Snek.Tests.Lexing;

public class LexerTests
{
    private readonly Lexer _lexer;
    private readonly CompilationContext _context;

    public LexerTests()
    {
        _lexer = new();
        _context = new("test.snek", new());
    }

    [Fact]
    public void Tokenize_Identifier_ReturnsIdentifierToken()
    {
        List<Token> tokens = [.. _lexer.Tokenize("myVar", _context)];

        tokens.Should().ContainSingle(t => t.Type == TokenType.Identifier);
        tokens.First(t => t.Type == TokenType.Identifier).Value.Should().Be("myVar");
    }

    [Fact]
    public void Tokenize_IntegerLiteral_ReturnsIntegerToken()
    {
        List<Token> tokens = [.. _lexer.Tokenize("42", _context)];

        Token token = tokens.First(t => t.Type == TokenType.IntegerLiteral);
        token.Value.Should().Be("42");
    }

    [Fact]
    public void Tokenize_FloatLiteral_ReturnsFloatToken()
    {
        List<Token> tokens = [.. _lexer.Tokenize("3.14", _context)];

        Token token = tokens.First(t => t.Type == TokenType.FloatLiteral);
        token.Value.Should().Be("3.14");
    }

    [Fact]
    public void Tokenize_StringLiteral_ReturnsStringToken()
    {
        List<Token> tokens = [.. _lexer.Tokenize("\"hello\"", _context)];

        Token token = tokens.First(t => t.Type == TokenType.StringLiteral);
        token.Value.Should().Be("hello");
    }

    [Fact]
    public void Tokenize_Keyword_ReturnsKeywordToken()
    {
        List<Token> tokens = [.. _lexer.Tokenize("fn", _context)];

        tokens.Should().Contain(t => t.Type == TokenType.KeywordFn);
    }

    [Fact]
    public void Tokenize_Operator_ReturnsCorrectOperatorToken()
    {
        List<Token> tokens = [.. _lexer.Tokenize("==", _context)];

        tokens.Should().Contain(t => t.Type == TokenType.DoubleEquals);
    }

    [Fact]
    public void Tokenize_WithComments_IgnoresComments()
    {
        List<Token> tokens = [.. _lexer.Tokenize("x # this is a comment\ny", _context)];

        List<string> identifiers = [.. tokens
            .Where(t => t.Type == TokenType.Identifier)
            .Select(t => t.Value)];

        identifiers.Should().Contain("x").And.Contain("y");
        tokens.Select(t => t.Value).Should().NotContain("this is a comment");
    }

    [Fact]
    public void Tokenize_WithIndentation_EmitsIndentDedentTokens()
    {
        string source = """
            fn main():
              x = 1
            """;
        List<Token> tokens = [.. _lexer.Tokenize(source, _context)];

        tokens.Should().Contain(t => t.Type == TokenType.Indent);
        tokens.Should().Contain(t => t.Type == TokenType.Dedent);
    }

    [Fact]
    public void Tokenize_UnterminatedString_ReportsError()
    {
        List<Token> tokens = [.. _lexer.Tokenize("\"unterminated", _context)];

        _context.Diagnostics.Should().Contain(d => d.IsError && d.Message.Contains("Unterminated"));
    }

    [Fact]
    public void Tokenize_Eof_ReturnsEofToken()
    {
        List<Token> tokens = [.. _lexer.Tokenize("", _context)];

        tokens.Should().Contain(t => t.Type == TokenType.Eof);
    }
}
```

---

### `Snek.Tests\Parsing\ParserTests.cs`

```csharp
using FluentAssertions;

namespace Snek.Tests.Parsing;

public class ParserTests
{
    private readonly Parser _parser;
    private readonly CompilationContext _context;

    public ParserTests()
    {
        _parser = new Parser();
        _context = new("test.snek", new());
    }

    private AstNode ParseSource(string source)
    {
        Lexer lexer = new();
        IEnumerable<Token> tokens = lexer.Tokenize(source, _context);
        return _parser.Parse(tokens, _context);
    }

    [Fact]
    public void Parse_FunctionDef_CreatesFunctionDefNode()
    {
        string source = """
            fn main():
              pass
            """;

        AstNode ast = ParseSource(source);

        ast.Should().BeOfType<ProgramNode>();
        ProgramNode program = (ProgramNode)ast;
        FunctionDefNode func = program.Statements.OfType<FunctionDefNode>().Should().ContainSingle().Subject;
        func.Name.Value.Should().Be("main");
        func.ReturnType.Should().BeNull();
    }

    [Fact]
    public void Parse_IfStatement_CreatesIfStatementNode()
    {
        string source = """
            if true:
              pass
            """;

        AstNode ast = ParseSource(source);

        ast.Should().BeOfType<ProgramNode>();
        ProgramNode program = (ProgramNode)ast;
        IfStatementNode ifStmt = program.Statements.OfType<IfStatementNode>().Should().ContainSingle().Subject;
        ifStmt.Condition.Should().BeOfType<LiteralExpressionNode>();
    }

    [Fact]
    public void Parse_WhileStatement_CreatesWhileStatementNode()
    {
        string source = """
            while x < 10:
              x = x + 1
            """;

        AstNode ast = ParseSource(source);

        ast.Should().BeOfType<ProgramNode>();
        ProgramNode program = (ProgramNode)ast;
        WhileStatementNode whileStmt = program.Statements.OfType<WhileStatementNode>().Should().ContainSingle().Subject;
        whileStmt.Condition.Should().BeOfType<BinaryExpressionNode>();
    }

    [Fact]
    public void Parse_ReturnStatement_CreatesReturnStatementNode()
    {
        string source = """
            fn foo() -> i32:
              return 42
            """;

        AstNode ast = ParseSource(source);

        ast.Should().BeOfType<ProgramNode>();
        ProgramNode program = (ProgramNode)ast;
        FunctionDefNode func = program.Statements.OfType<FunctionDefNode>().Should().ContainSingle().Subject;
        ReturnStatementNode returnStmt = func.Body.OfType<ReturnStatementNode>().Should().ContainSingle().Subject;
        returnStmt.Value.Should().BeOfType<LiteralExpressionNode>();
    }

    [Fact]
    public void Parse_CallExpression_CreatesCallExpressionNode()
    {
        string source = "print(\"hello\")";
        AstNode ast = ParseSource(source);

        ast.Should().BeOfType<ProgramNode>();
        ProgramNode program = (ProgramNode)ast;
        ExpressionStatementNode exprStmt = program.Statements.OfType<ExpressionStatementNode>().Should().ContainSingle().Subject;
        CallExpressionNode call = exprStmt.Expression.Should().BeOfType<CallExpressionNode>().Subject;
        ((IdentifierExpressionNode)call.Callee).Name.Value.Should().Be("print");
    }

    [Fact]
    public void Parse_BinaryExpression_CreatesBinaryExpressionNode()
    {
        string source = "x + y";
        AstNode ast = ParseSource(source);

        ast.Should().BeOfType<ProgramNode>();
        ProgramNode program = (ProgramNode)ast;
        ExpressionStatementNode exprStmt = program.Statements.OfType<ExpressionStatementNode>().Should().ContainSingle().Subject;
        BinaryExpressionNode binary = exprStmt.Expression.Should().BeOfType<BinaryExpressionNode>().Subject;
        binary.Operator.Type.Should().Be(TokenType.Plus);
    }

    [Fact]
    public void Parse_InvalidSyntax_ReportsError()
    {
        string source = "fn invalid(:";
        AstNode ast = ParseSource(source);

        _context.Diagnostics.Should().Contain(d => d.IsError);
    }

    [Fact]
    public void Parse_ParameterWithTypeAnnotation_ParsesCorrectly()
    {
        string source = "fn foo(x: i32):\n  pass";
        AstNode ast = ParseSource(source);

        ast.Should().BeOfType<ProgramNode>();
        ProgramNode program = (ProgramNode)ast;
        FunctionDefNode func = program.Statements.OfType<FunctionDefNode>().Should().ContainSingle().Subject;
        ParameterNode param = func.Parameters.Should().ContainSingle().Subject;
        param.Name.Value.Should().Be("x");
        param.TypeAnnotation?.Name.Value.Should().Be("i32");
    }
}
```

---

### `Snek.Tests\Pipeline\CompilerPipelineTests.cs`

```csharp
using FluentAssertions;
using Snek.Core.Analysis;
using Snek.Core.Generation;
using Snek.Core.Lexing;
using Snek.Core.Parsing;
using Snek.Core.Pipeline;

namespace Snek.Tests.Pipeline;

public class CompilerPipelineTests
{
    [Fact]
    public void Compile_ValidProgram_ReturnsSuccess()
    {
        CompilerPipeline pipeline = CreateDefaultPipeline();
        string source = "pass\n";

        CompilationResult result = pipeline.Compile(source, "test.snek");

        result.Success.Should().BeTrue();
        result.Output.Should().NotBeNull();
        result.Diagnostics.Should().NotContain(d => d.IsError);
    }

    [Fact]
    public void Compile_LexicalError_ReturnsFailure()
    {
        CompilerPipeline pipeline = CreateDefaultPipeline();
        string source = "\"unterminated";

        CompilationResult result = pipeline.Compile(source, "test.snek");

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.IsError && d.Message.Contains("Unterminated"));
    }

    [Fact]
    public void Compile_SyntaxError_ReturnsFailure()
    {
        CompilerPipeline pipeline = CreateDefaultPipeline();
        string source = "fn invalid(:";

        CompilationResult result = pipeline.Compile(source, "test.snek");

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.IsError);
    }

    [Fact]
    public void Compile_SemanticError_ReturnsFailure()
    {
        CompilerPipeline pipeline = CreateDefaultPipeline();
        string source = """
            fn foo() -> int:
              return "wrong"

            """;

        CompilationResult result = pipeline.Compile(source, "test.snek");

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.Message.Contains("Return type mismatch"));
    }

    [Fact]
    public void Compile_WithVerboseOption_LogsStages()
    {
        PipelineOptions options = new() { EnableLogging = true };
        CompilerPipeline pipeline = CreateDefaultPipeline(options);
        string source = "pass\n";

        CompilationResult result = pipeline.Compile(source, "test.snek");

        result.Success.Should().BeTrue();
        result.Output.Should().NotBeNull();
    }

    [Fact]
    public void Compile_AssemblyOutput_ContainsExpectedSections()
    {
        CompilerPipeline pipeline = CreateDefaultPipeline();
        string source = "print(\"hello\")\n";

        CompilationResult result = pipeline.Compile(source, "test.snek");

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("section '.data'");
        result.Output.Should().Contain("section '.text'");
        result.Output.Should().Contain("section '.idata'");
    }

    private static CompilerPipeline CreateDefaultPipeline(PipelineOptions? options = null)
    {
        return new(
            new Lexer(),
            new Parser(),
            new SemanticAnalyzer(),
            new CodeGenerator(),
            options);
    }
}
```

---

