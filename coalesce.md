### `Snek\Program.cs`

```csharp
using Snek.Analysis;
using Snek.Compiler;
using Snek.Generation;
using Snek.Lexer;
using Snek.Parser;
using Snek.Pipeline;

namespace Snek;

public class Program
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Snek Compiler v0.1");
            Console.WriteLine("Usage: snek <input.snek> [options]");
            Console.WriteLine("Options:");
            Console.WriteLine("  --output <file>  Specify output file (default: output.exe)");
            Console.WriteLine("  --syntax <name>  Use alternate syntax: python, cstyle (default: python)");
            Console.WriteLine("  --verbose        Enable detailed logging");
            Console.WriteLine("  --asm-only       Stop after generating assembly (do not assemble)");
            return;
        }

        var inputPath = args[0];
        var options = ParseOptions(args);

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Error: Input file not found: {inputPath}");
            return;
        }

        var source = File.ReadAllText(inputPath);
        var pipelineOptions = new PipelineOptions { EnableLogging = options.Verbose };

        var lexerRules = GetLexerRules(options.Syntax);
        var lexer = new SnekLexer(lexerRules);
        var parser = new SnekParser(lexerRules);
        var analyzer = new SnekSemanticAnalyzer();
        var generator = new SnekCodeGenerator();

        var pipeline = new CompilerPipeline(lexer, parser, analyzer, generator, pipelineOptions);
        var result = pipeline.Compile(source, inputPath);

        if (!result.Success)
        {
            foreach (var diag in result.Diagnostics)
            {
                Console.Error.WriteLine(diag.ToString());
            }
            return;
        }

        var asmOutputPath = options.OutputPath ?? "output.asm";
        var exeOutputPath = options.OutputPath?.Replace(".asm", ".exe") ?? "output.exe";

        File.WriteAllText(asmOutputPath, result.Output ?? string.Empty);
        Console.WriteLine($"Assembly generated: {asmOutputPath}");

        if (!options.AsmOnly)
        {
            var asmDirectory = Path.GetDirectoryName(Path.GetFullPath(asmOutputPath)) ?? ".";
            var assembler = new Assembler();

            if (assembler.Assemble(asmOutputPath, asmDirectory))
            {
                Console.WriteLine($"Executable created: {exeOutputPath}");
            }
            else
            {
                Console.Error.WriteLine("Assembly failed. Check FASM output above.");
                return;
            }
        }
    }

    private static CompilerOptions ParseOptions(string[] args)
    {
        var options = new CompilerOptions();
        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--output":
                    if (i + 1 < args.Length)
                    {
                        options.OutputPath = args[++i];
                    }
                    break;
                case "--syntax":
                    if (i + 1 < args.Length)
                    {
                        options.Syntax = args[++i];
                    }
                    break;
                case "--verbose":
                    options.Verbose = true;
                    break;
                case "--asm-only":
                    options.AsmOnly = true;
                    break;
            }
        }
        return options;
    }

    private static LexerRules GetLexerRules(string syntax)
    {
        return syntax?.ToLowerInvariant() switch
        {
            "cstyle" => LexerRules.CreateCStyle(),
            "python" => LexerRules.CreatePythonStyle(),
            _ => new LexerRules()
        };
    }
}

public record CompilerOptions
{
    public string? OutputPath { get; set; }
    public string? Syntax { get; set; }
    public bool Verbose { get; set; }
    public bool AsmOnly { get; set; }
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

</Project>
```

---

### `Snek\Analysis\ISemanticAnalyzer.cs`

```csharp
using Snek.Ast;
using Snek.Pipeline;

namespace Snek.Analysis;

/// <summary>
/// Abstract semantic analyzer contract. Validates AST semantics and builds symbol tables.
/// Swappable implementation enables different type systems or analysis strategies.
/// </summary>
public interface ISemanticAnalyzer
{
    /// <summary>
    /// Performs semantic analysis on the AST, populating context with diagnostics and symbol info.
    /// </summary>
    void Analyze(AstNode root, CompilationContext context);

    /// <summary>
    /// Resolves the type of an expression node within the given context.
    /// Returns the fully qualified type name or null if unresolvable.
    /// </summary>
    string? ResolveType(ExpressionNode expr, CompilationContext context);
}
```

---

### `Snek\Analysis\SnekSemanticAnalyzer.cs`

```csharp
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
```

---

### `Snek\Ast\AstNode.cs`

```csharp
namespace Snek.Ast;

/// <summary>
/// Base record for all AST nodes. Provides parent navigation for context-aware analysis.
/// </summary>
public abstract record AstNode
{
    public AstNode? Parent { get; set; }

    /// <summary>
    /// Yields all ancestor nodes from immediate parent to root.
    /// </summary>
    public IEnumerable<AstNode> Ancestors()
    {
        var current = Parent;
        while (current is not null)
        {
            yield return current;
            current = current.Parent;
        }
    }

    /// <summary>
    /// Finds the first ancestor of the specified type, or null.
    /// </summary>
    public T? AncestorOfType<T>() where T : AstNode
    {
        return Ancestors().OfType<T>().FirstOrDefault();
    }
}

// Placeholder for concrete node types to be added in subsequent iterations
public abstract record StatementNode : AstNode;
public abstract record ExpressionNode : AstNode;
public abstract record DeclarationNode : AstNode;
```

---

### `Snek\Ast\SnekAst.cs`

```csharp
using Snek.Lexer;

namespace Snek.Ast;

// Program root
public record ProgramNode(List<StatementNode> Statements) : AstNode;

// Statements
public record FunctionDefNode(
    Token Name,
    List<ParameterNode> Parameters,
    TypeNode? ReturnType,
    List<StatementNode> Body,
    int IndentLevel) : StatementNode;

public record ExpressionStatementNode(ExpressionNode Expression) : StatementNode;

public record IfStatementNode(
    ExpressionNode Condition,
    List<StatementNode> ThenBody,
    List<StatementNode>? ElseBody,
    int IndentLevel) : StatementNode;

public record WhileStatementNode(
    ExpressionNode Condition,
    List<StatementNode> Body,
    int IndentLevel) : StatementNode;

public record ReturnStatementNode(ExpressionNode? Value) : StatementNode;

public record PassStatementNode : StatementNode;

public record BreakStatementNode : StatementNode;

public record ContinueStatementNode : StatementNode;

// Expressions
public record CallExpressionNode(ExpressionNode Callee, List<ExpressionNode> Arguments) : ExpressionNode;

public record BinaryExpressionNode(ExpressionNode Left, Token Operator, ExpressionNode Right) : ExpressionNode;

public record UnaryExpressionNode(Token Operator, ExpressionNode Operand) : ExpressionNode;

public record MemberAccessExpressionNode(ExpressionNode Object, Token Member) : ExpressionNode;

public record IndexExpressionNode(ExpressionNode Target, ExpressionNode Index) : ExpressionNode;

public record LiteralExpressionNode(Token Value) : ExpressionNode;

public record IdentifierExpressionNode(Token Name) : ExpressionNode;

public record ListExpressionNode(List<ExpressionNode> Elements) : ExpressionNode;

public record DictExpressionNode(List<(ExpressionNode Key, ExpressionNode Value)> Items) : ExpressionNode;

// Parameters and Types
public record ParameterNode(Token Name, TypeNode? TypeAnnotation, ExpressionNode? Default) : AstNode;

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

### `Snek\Compiler\Assembler.cs`

```csharp
using System.Diagnostics;

namespace Snek.Compiler;

public class Assembler
{
    public bool Assemble(string asmPath, string outputDir)
    {
        string fasmPath = Path.Combine(AppContext.BaseDirectory, "fasm", "fasm.exe");

        if (!File.Exists(fasmPath))
        {
            Console.Error.WriteLine($"Error: FASM executable not found at '{fasmPath}'");
            Console.Error.WriteLine("Please ensure FASM is installed in the 'fasm' subdirectory.");
            return false;
        }

        try
        {
            Console.WriteLine("Executing FASM assembler...");

            var startInfo = new ProcessStartInfo
            {
                FileName = fasmPath,
                Arguments = $"\"{Path.GetFileName(asmPath)}\"",
                WorkingDirectory = outputDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Set FASM INCLUDE path if it exists alongside fasm.exe
            string fasmInclude = Path.Combine(Path.GetDirectoryName(fasmPath) ?? "", "INCLUDE");
            if (Directory.Exists(fasmInclude))
            {
                startInfo.EnvironmentVariables["INCLUDE"] = fasmInclude;
            }

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                Console.Error.WriteLine("Failed to start FASM process.");
                return false;
            }

            string output = process.StandardOutput.ReadToEnd();
            string errors = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(output))
            {
                Console.Write(output);
            }

            if (!string.IsNullOrWhiteSpace(errors))
            {
                Console.Error.Write(errors);
            }

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
}
```

---

### `Snek\Diagnoistics\Diagnostic.cs`

```csharp
namespace Snek.Diagnoistics;

/// <summary>
/// Represents a compiler diagnostic (error or warning) with source location.
/// </summary>
public record Diagnostic(
    string SourceName,
    string Message,
    int Line,
    int Column,
    DiagnosticSeverity Severity = DiagnosticSeverity.Error)
{
    public bool IsError => Severity == DiagnosticSeverity.Error;
    public bool IsWarning => Severity == DiagnosticSeverity.Warning;

    public override string ToString()
    {
        var prefix = Severity == DiagnosticSeverity.Error ? "error" : "warning";
        return $"{SourceName}({Line},{Column}): {prefix}: {Message}";
    }
}

public enum DiagnosticSeverity { Error, Warning }
```

---

### `Snek\Generation\ICodeGenerator.cs`

```csharp
using Snek.Ast;
using Snek.Pipeline;

namespace Snek.Generation;

/// <summary>
/// Abstract code generator contract. Converts analyzed AST to target output.
/// Swappable implementations enable multiple backends (x86, WASM, C, etc.).
/// </summary>
public interface ICodeGenerator
{
    /// <summary>
    /// Generates target code from the analyzed AST.
    /// Returns the generated output or null on failure.
    /// </summary>
    string? Generate(AstNode root, CompilationContext context);
}
```

---

### `Snek\Generation\SnekCodeGenerator.cs`

```csharp
using Snek.Ast;
using Snek.Lexer;
using Snek.Pipeline;
using System.Text;

namespace Snek.Generation;

public class SnekCodeGenerator : ICodeGenerator
{
    private readonly StringBuilder _output = new();
    private readonly Stack<string> _labelStack = new();
    private readonly Dictionary<string, string> _stringLiterals = [];
    private readonly HashSet<string> _externalFunctions = [];
    private int _labelCounter;
    private int _stringCounter;
    private CompilationContext _context = null!;

    public string? Generate(AstNode root, CompilationContext context)
    {
        _context = context;
        _ = _output.Clear();
        _labelStack.Clear();
        _stringLiterals.Clear();
        _externalFunctions.Clear();
        _labelCounter = 0;
        _stringCounter = 0;

        if (root is not ProgramNode program)
        {
            return null;
        }

        // First pass: collect string literals and external function references
        CollectStringsAndExternals(program);

        EmitHeader();
        EmitDataSection();
        EmitImportSection();
        EmitTextSectionHeader();
        EmitEntryPoint();

        foreach (var stmt in program.Statements)
        {
            if (stmt is FunctionDefNode func)
            {
                EmitFunction(func);
            }
        }

        return _output.ToString();
    }

    private void CollectStringsAndExternals(AstNode node)
    {
        if (node is LiteralExpressionNode lit && lit.Value.Type == TokenType.StringLiteral)
        {
            if (!_stringLiterals.ContainsValue(lit.Value.Value))
            {
                _stringLiterals[$"str{_stringCounter++}"] = lit.Value.Value;
            }
        }
        else if (node is CallExpressionNode call && call.Callee is IdentifierExpressionNode id)
        {
            if (id.Name.Value is not "main" and not "print")
            {
                _ = _externalFunctions.Add(id.Name.Value);
            }
        }

        foreach (var prop in node.GetType().GetProperties())
        {
            if (prop.Name == "Parent")
            {
                continue;
            }

            var value = prop.GetValue(node);
            if (value is AstNode child)
            {
                CollectStringsAndExternals(child);
            }
            else if (value is IEnumerable<AstNode> children)
            {
                foreach (var c in children)
                {
                    CollectStringsAndExternals(c);
                }
            }
        }
    }

    private void EmitHeader()
    {
        _ = _output.AppendLine("format PE console");
        _ = _output.AppendLine("entry start");
        _ = _output.AppendLine();
        _ = _output.AppendLine("include 'win32a.inc'");
        _ = _output.AppendLine();
    }

    private void EmitDataSection()
    {
        if (_stringLiterals.Count == 0)
        {
            return;
        }

        _ = _output.AppendLine("section '.data' data readable writeable");
        foreach (var (label, value) in _stringLiterals)
        {
            _ = _output.Append($"    {label} db ");
            var parts = new List<string>();
            foreach (char c in value)
            {
                if (c is '\n' or '\t' or '\r' or '\'' or '"')
                {
                    if (parts.Count > 0 && !parts[^1].EndsWith("'"))
                    {
                        parts[^1] += "',";
                    }

                    parts.Add(((byte)c).ToString());
                }
                else
                {
                    if (parts.Count == 0 || parts[^1].StartsWith((char)(byte)0) || parts[^1].EndsWith(","))
                    {
                        parts.Add("'");
                    }

                    parts[^1] += c;
                }
            }
            if (parts.Count > 0 && !parts[^1].EndsWith("'"))
            {
                parts[^1] += "'";
            }

            parts.Add("0");
            _ = _output.AppendLine(string.Join(",", parts));
        }
        _ = _output.AppendLine();
    }

    private void EmitImportSection()
    {
        _ = _output.AppendLine("section '.idata' import data readable");
        _ = _output.AppendLine();

        var libs = new Dictionary<string, HashSet<string>>
        {
            ["kernel32.dll"] = ["ExitProcess"],
            ["msvcrt.dll"] = ["printf"]
        };

        foreach (var func in _externalFunctions)
        {
            _ = libs["msvcrt.dll"].Add(func);
        }

        var libDefs = libs.Keys.Select(lib => $"{lib.Split('.')[0]},'{lib}'");
        _ = _output.AppendLine($"    library {string.Join(",", libDefs)}");
        _ = _output.AppendLine();

        foreach (var (libName, functions) in libs.OrderBy(k => k.Key))
        {
            var alias = libName.Split('.')[0];
            var imports = functions.OrderBy(f => f).Select(f => $"{f},'{f}'");
            _ = _output.AppendLine($"    import {alias}, {string.Join(",", imports)}");
        }
        _ = _output.AppendLine();
    }

    private void EmitTextSectionHeader()
    {
        _ = _output.AppendLine("section '.text' code readable executable");
        _ = _output.AppendLine();
    }

    private void EmitEntryPoint()
    {
        _ = _output.AppendLine("start:");
        _ = _output.AppendLine("    call _main");
        _ = _output.AppendLine("    push eax");
        _ = _output.AppendLine("    call [ExitProcess]");
        _ = _output.AppendLine();
    }

    private void EmitFunction(FunctionDefNode func)
    {
        string mangledName = func.Name.Value == "main" ? "_main" : func.Name.Value;
        _ = _output.AppendLine($"{mangledName}:");
        _ = _output.AppendLine("    push ebp");
        _ = _output.AppendLine("    mov ebp, esp");

        int paramOffset = 8;
        foreach (var param in func.Parameters)
        {
            _ = _output.AppendLine($"    ; param {param.Name.Value} at [ebp+{paramOffset}]");
            paramOffset += 4;
        }

        foreach (var stmt in func.Body)
        {
            EmitStatement(stmt);
        }

        if (func.ReturnType?.Name.Value == "void")
        {
            _ = _output.AppendLine("    xor eax, eax");
        }

        _ = _output.AppendLine("    leave");
        _ = _output.AppendLine("    ret");
        _ = _output.AppendLine();
    }

    private void EmitStatement(StatementNode stmt)
    {
        switch (stmt)
        {
            case ExpressionStatementNode expr:
                EmitExpression(expr.Expression);
                _ = _output.AppendLine("    pop eax");
                break;
            case ReturnStatementNode ret:
                if (ret.Value != null)
                {
                    EmitExpression(ret.Value);
                }
                else
                {
                    _ = _output.AppendLine("    xor eax, eax");
                }
                break;
            case IfStatementNode ifs:
                EmitIf(ifs);
                break;
            case WhileStatementNode whl:
                EmitWhile(whl);
                break;
        }
    }

    private void EmitIf(IfStatementNode ifs)
    {
        var elseLabel = $"_else_{_labelCounter++}";
        var endLabel = $"_endif_{_labelCounter}";

        EmitExpression(ifs.Condition);
        _ = _output.AppendLine("    pop eax");
        _ = _output.AppendLine("    test eax, eax");
        _ = _output.AppendLine($"    jz {elseLabel}");

        foreach (var s in ifs.ThenBody)
        {
            EmitStatement(s);
        }

        _ = _output.AppendLine($"    jmp {endLabel}");
        _ = _output.AppendLine($"{elseLabel}:");
        if (ifs.ElseBody != null)
        {
            foreach (var s in ifs.ElseBody)
            {
                EmitStatement(s);
            }
        }

        _ = _output.AppendLine($"{endLabel}:");
    }

    private void EmitWhile(WhileStatementNode whl)
    {
        var startLabel = $"_while_{_labelCounter}";
        var endLabel = $"_endwhile_{_labelCounter++}";

        _ = _output.AppendLine($"{startLabel}:");
        EmitExpression(whl.Condition);
        _ = _output.AppendLine("    pop eax");
        _ = _output.AppendLine("    test eax, eax");
        _ = _output.AppendLine($"    jz {endLabel}");

        foreach (var s in whl.Body)
        {
            EmitStatement(s);
        }

        _ = _output.AppendLine($"    jmp {startLabel}");
        _ = _output.AppendLine($"{endLabel}:");
    }

    private void EmitExpression(ExpressionNode expr)
    {
        switch (expr)
        {
            case LiteralExpressionNode lit:
                EmitLiteral(lit);
                break;
            case IdentifierExpressionNode id:
                _ = _output.AppendLine($"    ; load {id.Name.Value}");
                _ = _output.AppendLine("    push 0");
                break;
            case CallExpressionNode call:
                EmitCall(call);
                break;
            case BinaryExpressionNode bin:
                EmitBinary(bin);
                break;
        }
    }

    private void EmitLiteral(LiteralExpressionNode lit)
    {
        switch (lit.Value.Type)
        {
            case TokenType.StringLiteral:
                var label = _stringLiterals.First(kvp => kvp.Value == lit.Value.Value).Key;
                _ = _output.AppendLine($"    push {label}");
                break;
            case TokenType.IntegerLiteral:
                _ = _output.AppendLine($"    push {lit.Value.Value}");
                break;
            case TokenType.KeywordTrue:
                _ = _output.AppendLine("    push 1");
                break;
            case TokenType.KeywordFalse:
                _ = _output.AppendLine("    push 0");
                break;
        }
    }

    private void EmitCall(CallExpressionNode call)
    {
        for (int i = call.Arguments.Count - 1; i >= 0; i--)
        {
            EmitExpression(call.Arguments[i]);
        }

        var callee = call.Callee is IdentifierExpressionNode id ? id.Name.Value : "unknown";
        string target;

        if (callee == "print")
        {
            target = "[printf]";
            _ = _externalFunctions.Add("printf");
        }
        else if (callee == "main")
        {
            target = "_main";
        }
        else
        {
            target = _externalFunctions.Contains(callee) ? $"[{callee}]" : callee;
        }

        _ = _output.AppendLine($"    call {target}");

        if (call.Arguments.Count > 0)
        {
            _ = _output.AppendLine($"    add esp, {call.Arguments.Count * 4}");
        }

        _ = _output.AppendLine("    push eax");
    }

    private void EmitBinary(BinaryExpressionNode bin)
    {
        EmitExpression(bin.Right);
        EmitExpression(bin.Left);
        _ = _output.AppendLine("    pop ebx");
        _ = _output.AppendLine("    pop eax");

        switch (bin.Operator.Type)
        {
            case TokenType.Plus:
                _ = _output.AppendLine("    add eax, ebx");
                break;
            case TokenType.Minus:
                _ = _output.AppendLine("    sub eax, ebx");
                break;
            case TokenType.Star:
                _ = _output.AppendLine("    imul eax, ebx");
                break;
            case TokenType.DoubleEquals:
                _ = _output.AppendLine("    cmp eax, ebx");
                _ = _output.AppendLine("    sete al");
                _ = _output.AppendLine("    movzx eax, al");
                break;
            default:
                _ = _output.AppendLine("    ; unsupported binary op");
                break;
        }
        _ = _output.AppendLine("    push eax");
    }
}
```

---

### `Snek\Lexer\ILexer.cs`

```csharp
using Snek.Pipeline;

namespace Snek.Lexer;

/// <summary>
/// Abstract lexer contract. Implementations define syntax-specific tokenization rules.
/// Swapping implementations changes the language syntax without affecting downstream stages.
/// </summary>
public interface ILexer
{
    /// <summary>
    /// Converts source text into a stream of tokens.
    /// Reports lexical errors via the context.
    /// </summary>
    IEnumerable<Token> Tokenize(string source, CompilationContext context);
}
```

---

### `Snek\Lexer\LexerRules.cs`

```csharp
namespace Snek.Lexer;

///  <summary >
/// Declarative rules for token recognition. Enables syntax customization without code changes.
///  </summary >
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
        Keywords["void"] = TokenType.KeywordVoid;
        Keywords["int"] = TokenType.KeywordInt;
        Keywords["string"] = TokenType.KeywordString;
        Keywords["bool"] = TokenType.KeywordBool;
        Keywords["float"] = TokenType.KeywordFloat;
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

    ///  <summary >
    /// Creates a rule set for C-style syntax (fn main() -> void { ... })
    ///  </summary >
    public static LexerRules CreateCStyle()
    {
        var rules = new LexerRules
        {
            SupportsIndentation = false,
            StringDelimiter = '"',
            CharDelimiter = '\''
        };
        // Reuse keywords, just change structural expectations in parser
        return rules;
    }

    ///  <summary >
    /// Creates a rule set for Python-style syntax (def main(): ...)
    ///  </summary >
    public static LexerRules CreatePythonStyle()
    {
        var rules = new LexerRules();
        rules.Keywords["def"] = TokenType.KeywordDef;
        return rules;
    }
}
```

---

### `Snek\Lexer\SnekLexer.cs`

```csharp
using Snek.Diagnoistics;
using Snek.Pipeline;
using System.Text;

namespace Snek.Lexer;

/// <summary>
/// Reference lexer for Snek's default Python-like syntax.
/// Pluggable via ILexer interface for syntax variants.
/// </summary>
public class SnekLexer : ILexer
{
    private readonly LexerRules _rules;
    private string _source = string.Empty;
    private int _position;
    private int _line = 1;
    private int _column = 1;
    private CompilationContext? _context;
    private readonly Stack<int> _indentStack = new();

    public SnekLexer(LexerRules? rules = null)
    {
        _rules = rules ?? new LexerRules();
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

        var tokens = new List<Token>();

        while (!IsAtEnd())
        {
            SkipWhitespaceAndComments();
            if (IsAtEnd())
            {
                break;
            }

            var startLine = _line;
            var startColumn = _column;

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
            _ = Advance();
        }

        // Emit dedents to close all indentation levels
        while (_indentStack.Count > 1)
        {
            _ = _indentStack.Pop();
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
        return _position + offset < _source.Length ? _source[_position + offset] : '\0';
    }

    private char Advance()
    {
        var c = _source[_position++];
        if (c == '\n') { _line++; _column = 1; }
        else { _column++; }
        return c;
    }

    private void SkipWhitespaceAndComments()
    {
        while (!IsAtEnd())
        {
            var c = Peek();
            if (char.IsWhiteSpace(c) && c != '\n') { _ = Advance(); continue; }
            if (c == '#') { while (!IsAtEnd() && Peek() != '\n') { _ = Advance(); } continue; }
            break;
        }
    }

    private bool TryReadKeywordOrIdentifier(List<Token> tokens)
    {
        if (!char.IsLetter(Peek()) && Peek() != '_' && !_rules.IdentifierStartChars.Contains(Peek()))
        {
            return false;
        }

        var startLine = _line;
        var startColumn = _column;
        var sb = new StringBuilder();

        while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '_' || _rules.IdentifierContinueChars.Contains(Peek())))
        {
            _ = sb.Append(Advance());
        }

        var value = sb.ToString();
        var type = _rules.Keywords.TryGetValue(value, out var keywordType) ? keywordType : TokenType.Identifier;
        tokens.Add(new Token(type, value, startLine, startColumn));
        return true;
    }

    private bool TryReadNumber(List<Token> tokens)
    {
        if (!char.IsDigit(Peek()))
        {
            return false;
        }

        var startLine = _line;
        var startColumn = _column;
        var sb = new StringBuilder();
        bool isFloat = false;

        // Integer part
        while (char.IsDigit(Peek()))
        {
            _ = sb.Append(Advance());
        }

        // Fractional part
        if (Peek() == '.' && char.IsDigit(Peek(1)))
        {
            isFloat = true;
            _ = sb.Append(Advance()); // .
            while (char.IsDigit(Peek()))
            {
                _ = sb.Append(Advance());
            }
        }

        // Exponent
        if (Peek() is 'e' or 'E')
        {
            isFloat = true;
            _ = sb.Append(Advance());
            if (Peek() is '+' or '-')
            {
                _ = sb.Append(Advance());
            }

            while (char.IsDigit(Peek()))
            {
                _ = sb.Append(Advance());
            }
        }

        var value = sb.ToString();
        var type = isFloat ? TokenType.FloatLiteral : TokenType.IntegerLiteral;
        tokens.Add(new Token(type, value, startLine, startColumn));
        return true;
    }

    private bool TryReadString(List<Token> tokens)
    {
        var c = Peek();
        if (c != _rules.StringDelimiter && c != _rules.CharDelimiter)
        {
            return false;
        }

        var startLine = _line;
        var startColumn = _column;
        var delimiter = Advance(); // consume opening quote
        var isChar = delimiter == _rules.CharDelimiter;
        var sb = new StringBuilder();

        while (!IsAtEnd() && Peek() != delimiter)
        {
            var ch = Advance();
            if (ch == '\\')
            {
                if (IsAtEnd())
                {
                    break;
                }

                var escaped = Advance();
                _ = sb.Append(escaped switch
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
                _ = sb.Append(ch);
            }
        }

        if (IsAtEnd() || Peek() != delimiter)
        {
            ReportError("Unterminated string literal", startLine, startColumn);
            return true;
        }
        _ = Advance(); // consume closing quote

        var type = isChar ? TokenType.CharLiteral : TokenType.StringLiteral;
        tokens.Add(new Token(type, sb.ToString(), startLine, startColumn));
        return true;
    }

    private bool TryReadOperator(List<Token> tokens)
    {
        // Try longest operators first
        foreach (var (pattern, type) in _rules.Operators.OrderByDescending(o => o.Pattern.Length))
        {
            if (MatchString(pattern))
            {
                var startLine = _line;
                var startColumn = _column;
                // Advance past the matched pattern
                for (int i = 0; i < pattern.Length; i++)
                {
                    _ = Advance();
                }

                tokens.Add(new Token(type, pattern, startLine, startColumn));
                return true;
            }
        }
        return false;
    }

    private bool TryReadStructural(List<Token> tokens)
    {
        var c = Peek();
        var startLine = _line;
        var startColumn = _column;

        if (c == '\n')
        {
            _ = Advance();
            HandleNewline(tokens, startLine, startColumn);
            return true;
        }

        // Single-char structural tokens not in operators list
        if (c is '(' or ')' or '[' or ']' or '{' or '}' or ',' or '.' or ':')
        {
            _ = Advance();
            var type = c switch
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
        var indent = 0;
        var tempPos = _position;
        while (tempPos < _source.Length)
        {
            var c = _source[tempPos];
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

        var currentIndent = _indentStack.Peek();

        if (indent > currentIndent)
        {
            _indentStack.Push(indent);
            tokens.Add(new Token(TokenType.Indent, "", line, column));
        }
        else if (indent < currentIndent)
        {
            while (_indentStack.Count > 1 && _indentStack.Peek() > indent)
            {
                _ = _indentStack.Pop();
                tokens.Add(new Token(TokenType.Dedent, "", line, column));
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
            if (_source[_position + i] != expected[i])
            {
                return false;
            }
        }

        return true;
    }

    private void ReportError(string message, int line, int column)
    {
        _context?.Diagnostics.Add(new Diagnostic(_context.SourceName, message, line, column, DiagnosticSeverity.Error));
    }
}
```

---

### `Snek\Lexer\Token.cs`

```csharp
namespace Snek.Lexer;

/// <summary>
/// Represents a lexical token with type, value, and source position.
/// </summary>
public record Token(TokenType Type, string Value, int Line, int Column)
{
    public override string ToString()
    {
        return $"[{Line}:{Column}] {Type}='{Value}'";
    }
}
```

---

### `Snek\Lexer\TokenType.cs`

```csharp
namespace Snek.Lexer;

/// <summary>
/// Token categories. Extensible for syntax variants via partial classes or enums.
/// </summary>
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
    KeywordVoid,
    KeywordInt,
    KeywordString,
    KeywordBool,
    KeywordFloat,
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

### `Snek\Parser\ExpressionParser.cs`

```csharp
using Snek.Ast;
using Snek.Lexer;

namespace Snek.Parser;

public class ExpressionParser
{
    private readonly ParserStream _stream;

    public ExpressionParser(ParserStream stream)
    {
        _stream = stream;
    }

    public ExpressionNode ParseExpression(int precedence = 0)
    {
        var left = ParsePrimary();

        while (true)
        {
            var op = _stream.Current;
            int nextPrecedence = GetPrecedence(op.Type);
            if (nextPrecedence < precedence)
            {
                break;
            }

            _stream.Advance();
            var right = ParseExpression(nextPrecedence + 1);
            left = new BinaryExpressionNode(left, op, right);
        }

        return left;
    }

    private ExpressionNode ParsePrimary()
    {
        if (_stream.Match(TokenType.Identifier))
        {
            var name = _stream.Previous;
            if (_stream.Match(TokenType.LeftParen))
            {
                return ParseCall(name);
            }

            if (_stream.Match(TokenType.Dot))
            {
                return ParseMemberAccess(name);
            }

            return _stream.Match(TokenType.LeftBracket) ? ParseIndex(name) : (ExpressionNode)new IdentifierExpressionNode(name);
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
            var expr = ParseExpression();
            _ = _stream.Consume(TokenType.RightParen);
            return expr;
        }

        if (_stream.Match(TokenType.Minus) || _stream.Match(TokenType.KeywordNot))
        {
            var op = _stream.Previous;
            var operand = ParsePrimary();
            return new UnaryExpressionNode(op, operand);
        }

        if (_stream.Match(TokenType.LeftBracket))
        {
            return ParseListLiteral();
        }

        _stream.ReportError($"Unexpected token in expression: '{_stream.Current.Type}'", _stream.Current);
        _stream.Advance();
        return new LiteralExpressionNode(new Token(TokenType.Unknown, "unknown", -1, -1));
    }

    private CallExpressionNode ParseCall(Token callee)
    {
        var args = new List<ExpressionNode>();
        if (!_stream.Match(TokenType.RightParen))
        {
            do
            {
                args.Add(ParseExpression());
            } while (_stream.Match(TokenType.Comma));
            _ = _stream.Consume(TokenType.RightParen);
        }
        return new CallExpressionNode(new IdentifierExpressionNode(callee), args);
    }

    private MemberAccessExpressionNode ParseMemberAccess(Token obj)
    {
        var member = _stream.Consume(TokenType.Identifier);
        return new MemberAccessExpressionNode(new IdentifierExpressionNode(obj), member);
    }

    private IndexExpressionNode ParseIndex(Token target)
    {
        var index = ParseExpression();
        _ = _stream.Consume(TokenType.RightBracket);
        return new IndexExpressionNode(new IdentifierExpressionNode(target), index);
    }

    private ListExpressionNode ParseListLiteral()
    {
        var elements = new List<ExpressionNode>();
        if (!_stream.Match(TokenType.RightBracket))
        {
            do
            {
                elements.Add(ParseExpression());
            } while (_stream.Match(TokenType.Comma));
            _ = _stream.Consume(TokenType.RightBracket);
        }
        return new ListExpressionNode(elements);
    }

    private int GetPrecedence(TokenType type)
    {
        return type switch
        {
            TokenType.KeywordOr => 1,
            TokenType.KeywordAnd => 2,
            TokenType.DoubleEquals or TokenType.NotEquals or TokenType.LessThan or TokenType.GreaterThan
                or TokenType.LessEqual or TokenType.GreaterEqual => 3,
            TokenType.Plus or TokenType.Minus => 4,
            TokenType.Star or TokenType.Slash or TokenType.Percent => 5,
            TokenType.DoubleStar => 6,
            _ => -1
        };
    }
}
```

---

### `Snek\Parser\IParser.cs`

```csharp
using Snek.Ast;
using Snek.Lexer;
using Snek.Pipeline;

namespace Snek.Parser;

/// <summary>
/// Abstract parser contract. Implementations convert tokens to AST based on syntax rules.
/// Swapping implementations changes language grammar without affecting lexer or analyzer.
/// </summary>
public interface IParser
{
    /// <summary>
    /// Parses a token stream into an AST. Reports errors via context.
    /// </summary>
    AstNode Parse(IEnumerable<Token> tokens, CompilationContext context);
}
```

---

### `Snek\Parser\ParserExtensions.cs`

```csharp
using Snek.Lexer;
using Snek.Pipeline;

namespace Snek.Parser;

/// <summary>
/// Extension methods for token stream parsing patterns.
/// Keeps parser implementations clean and reusable.
/// </summary>
public static class ParserExtensions
{
    /// <summary>
    /// Parses a comma-separated list of items until the terminator token.
    /// </summary>
    public static List<T> ParseCommaSeparated<T>(
        this IEnumerator<Token> tokens,
        TokenType terminator,
        Func<Token, CompilationContext, T> parseItem,
        CompilationContext context)
    {
        var items = new List<T>();
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
            _ = tokens.MoveNext();
        } while (tokens.Current?.Type == TokenType.Comma);

        return items;
    }

    /// <summary>
    /// Skips tokens until a synchronization point (newline, dedent, or specific token).
    /// Used for error recovery.
    /// </summary>
    public static void SkipToSyncPoint(
        this IEnumerator<Token> tokens,
        params TokenType[] syncPoints)
    {
        while (tokens.Current?.Type is not (TokenType.Eof or TokenType.Newline or TokenType.Dedent)
               && !syncPoints.Contains(tokens.Current.Type))
        {
            _ = tokens.MoveNext();
        }
    }

    /// <summary>
    /// Peeks ahead N tokens without consuming them.
    /// </summary>
    public static Token? Peek(this IEnumerator<Token> tokens, int offset = 0)
    {
        _ = tokens.Current;
        for (int i = 0; i <= offset && tokens.MoveNext(); i++)
        {
            if (i == offset)
            {
                return tokens.Current;
            }
        }
        return null;
    }
}
```

---

### `Snek\Parser\ParserStream.cs`

```csharp
using Snek.Diagnoistics;
using Snek.Lexer;
using Snek.Pipeline;

namespace Snek.Parser;

public class ParserStream
{
    private readonly IEnumerator<Token> _tokens;
    private readonly CompilationContext _context;

    public Token Current { get; private set; }
    public Token Previous { get; private set; }

    public ParserStream(IEnumerable<Token> tokens, CompilationContext context)
    {
        _tokens = tokens.GetEnumerator();
        _context = context;
        Current = new Token(TokenType.Eof, " ", -1, -1);
        Previous = Current;
        Advance(); // Initialize Current
    }

    public void Advance()
    {
        Previous = Current;
        Current = _tokens.MoveNext() ? _tokens.Current : new Token(TokenType.Eof, " ", -1, -1);
    }

    public bool Match(TokenType type)
    {
        if (Current.Type == type)
        {
            Advance();
            return true;
        }
        return false;
    }

    public Token Consume(TokenType type)
    {
        if (Current.Type == type)
        {
            var token = Current;
            Advance();
            return token;
        }

        ReportError($"Expected '{type}' but got '{Current.Type}'", Current);
        return Current;
    }

    public void ReportError(string message, Token atToken)
    {
        _context.Diagnostics.Add(new Diagnostic(
            _context.SourceName,
            message,
            atToken.Line,
            atToken.Column,
            DiagnosticSeverity.Error));
    }
}
```

---

### `Snek\Parser\SnekParser.cs`

```csharp
using Snek.Ast;
using Snek.Lexer;
using Snek.Pipeline;

namespace Snek.Parser;

/// <summary>
/// Orchestrator for the parsing process.
/// Delegates actual parsing logic to specialized StatementParser and ExpressionParser components.
/// </summary>
public class SnekParser : IParser
{
    private readonly LexerRules _rules;

    public SnekParser(LexerRules? rules = null)
    {
        _rules = rules ?? new LexerRules();
    }

    public AstNode Parse(IEnumerable<Token> tokens, CompilationContext context)
    {
        var stream = new ParserStream(tokens, context);
        var expressionParser = new ExpressionParser(stream);
        var statementParser = new StatementParser(stream, expressionParser, _rules);

        return statementParser.ParseProgram();
    }
}
```

---

### `Snek\Parser\StatementParser.cs`

```csharp
using Snek.Ast;
using Snek.Lexer;

namespace Snek.Parser;

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
        var statements = new List<StatementNode>();
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

            var stmt = ParseStatement();
            if (stmt != null)
            {
                statements.Add(stmt);
            }
        }
        return new ProgramNode(statements);
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

        var expr = _expressions.ParseExpression();
        ExpectNewline();
        return new ExpressionStatementNode(expr);
    }

    private FunctionDefNode ParseFunctionDef()
    {
        var name = _stream.Consume(TokenType.Identifier);
        _ = _stream.Consume(TokenType.LeftParen);
        var parameters = ParseParameters();

        TypeNode? returnType = null;
        if (_stream.Match(TokenType.Arrow))
        {
            returnType = ParseTypeAnnotation();
        }

        _ = _stream.Consume(TokenType.Colon);
        ExpectNewline();

        var bodyIndent = _expectedIndent + _rules.TabWidth;
        var body = ParseIndentedBlock();

        return new FunctionDefNode(name, parameters, returnType, body, bodyIndent);
    }

    private List<ParameterNode> ParseParameters()
    {
        var parameters = new List<ParameterNode>();
        if (_stream.Match(TokenType.RightParen))
        {
            return parameters;
        }

        do
        {
            var paramName = _stream.Consume(TokenType.Identifier);
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
            parameters.Add(new ParameterNode(paramName, typeAnn, defaultValue));
        } while (_stream.Match(TokenType.Comma));

        _ = _stream.Consume(TokenType.RightParen);
        return parameters;
    }

    private TypeNode ParseTypeAnnotation()
    {
        Token nameToken = _stream.Match(TokenType.Identifier)
            ? _stream.Previous
            : _stream.Match(TokenType.KeywordVoid) || _stream.Match(TokenType.KeywordInt) ||
                 _stream.Match(TokenType.KeywordString) || _stream.Match(TokenType.KeywordBool) ||
                 _stream.Match(TokenType.KeywordFloat)
                ? _stream.Previous
                : _stream.Consume(TokenType.Identifier);

        if (_stream.Match(TokenType.LessThan))
        {
            var args = new List<TypeNode>();
            do { args.Add(ParseTypeAnnotation()); } while (_stream.Match(TokenType.Comma));
            _ = _stream.Consume(TokenType.GreaterThan);
            return TypeNode.Generic(nameToken, args);
        }
        return TypeNode.Simple(nameToken);
    }

    private IfStatementNode ParseIfStatement()
    {
        var condition = _expressions.ParseExpression();
        _ = _stream.Consume(TokenType.Colon);
        ExpectNewline();

        var thenIndent = _expectedIndent + _rules.TabWidth;
        var thenBody = ParseIndentedBlock();

        List<StatementNode>? elseBody = null;
        if (_stream.Match(TokenType.KeywordElse))
        {
            _ = _stream.Consume(TokenType.Colon);
            ExpectNewline();
            _ = _expectedIndent + _rules.TabWidth;
            elseBody = ParseIndentedBlock();
        }

        return new IfStatementNode(condition, thenBody, elseBody, thenIndent);
    }

    private WhileStatementNode ParseWhileStatement()
    {
        var condition = _expressions.ParseExpression();
        _ = _stream.Consume(TokenType.Colon);
        ExpectNewline();

        var bodyIndent = _expectedIndent + _rules.TabWidth;
        var body = ParseIndentedBlock();

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
        _ = _stream.Consume(TokenType.Indent);
        var statements = new List<StatementNode>();

        while (!_stream.Match(TokenType.Dedent) && !_stream.Match(TokenType.Eof))
        {
            if (_stream.Match(TokenType.Newline))
            {
                continue;
            }

            var stmt = ParseStatement();
            if (stmt != null)
            {
                statements.Add(stmt);
            }
        }
        return statements;
    }

    private void ExpectNewline()
    {
        if (!_stream.Match(TokenType.Newline) && !_stream.Match(TokenType.Eof))
        {
            _stream.ReportError($"Expected newline after statement, got '{_stream.Current.Type}'", _stream.Current);
        }
    }
}
```

---

### `Snek\Pipeline\CompilationContext.cs`

```csharp
using Snek.Diagnoistics;

namespace Snek.Pipeline;

/// <summary>
/// Mutable context object shared across all pipeline stages.
/// Holds diagnostics, configuration, and stage-specific caches.
/// </summary>
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
        return StageData.TryGetValue(key, out var value) ? value as T : null;
    }

    public void SetStageData<T>(string key, T value) where T : class
    {
        StageData[key] = value;
    }
}
```

---

### `Snek\Pipeline\CompilerPipeline.cs`

```csharp
using Snek.Analysis;
using Snek.Diagnoistics;
using Snek.Generation;
using Snek.Lexer;
using Snek.Parser;

namespace Snek.Pipeline;

/// <summary>
/// Modular compilation pipeline where each stage is swappable via dependency injection.
/// Enables syntax-agnostic compilation by abstracting lexer/parser behind interfaces.
/// </summary>
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
        var context = new CompilationContext(sourceName, _options);

        try
        {
            // Stage 1: Lexing
            if (_options.EnableLogging)
            {
                Console.WriteLine($"[{sourceName}] Lexing...");
            }

            var tokens = _lexer.Tokenize(source, context);
            if (context.Diagnostics.Any(d => d.IsError))
            {
                return new CompilationResult(context.Diagnostics);
            }

            // Stage 2: Parsing
            if (_options.EnableLogging)
            {
                Console.WriteLine($"[{sourceName}] Parsing...");
            }

            var ast = _parser.Parse(tokens, context);
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

            var output = _generator.Generate(ast, context);
            return context.Diagnostics.Any(d => d.IsError)
                ? new CompilationResult(context.Diagnostics)
                : new CompilationResult(output, context.Diagnostics);
        }
        catch (Exception ex)
        {
            context.Diagnostics.Add(new Diagnostic(sourceName, $"Internal compiler error: {ex.Message}", -1, -1, DiagnosticSeverity.Error));
            return new CompilationResult(context.Diagnostics);
        }
    }
}

public record PipelineOptions
{
    public bool EnableLogging { get; set; } = false;
    public bool EnableOptimizations { get; set; } = false;
    public TargetPlatform Target { get; set; } = TargetPlatform.X86;
}

public enum TargetPlatform { X86, X64, WebAssembly }

public record CompilationResult(string? Output, IReadOnlyList<Diagnostic> Diagnostics)
{
    public CompilationResult(IReadOnlyList<Diagnostic> diagnostics) : this(null, diagnostics) { }
    public bool Success => !Diagnostics.Any(d => d.IsError);
}
```

---

### `Snek\Pipeline\IPipelineStage.cs`

```csharp
using Snek.Diagnoistics;

namespace Snek.Pipeline;

/// <summary>
/// Base contract for any pipeline stage. Enables uniform error handling and logging.
/// </summary>
public interface IPipelineStage
{
    /// <summary>
    /// Executes the stage, mutating the context and returning success status.
    /// </summary>
    bool Execute(CompilationContext context);

    /// <summary>
    /// Optional: collects diagnostics produced by this stage.
    /// </summary>
    IReadOnlyList<Diagnostic> GetDiagnostics();
}
```

---

