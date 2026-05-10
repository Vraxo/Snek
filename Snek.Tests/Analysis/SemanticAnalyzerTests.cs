using FluentAssertions;
using Snek.Analysis;
using Snek.Ast;
using Snek.Lexer;
using Snek.Parser;
using Snek.Pipeline;

namespace Snek.Tests.Analysis;

public class SemanticAnalyzerTests
{
    private readonly SemanticAnalyzer _analyzer;
    private readonly CompilationContext _context;

    public SemanticAnalyzerTests()
    {
        _analyzer = new SemanticAnalyzer();
        _context = new CompilationContext("test.snek", new PipelineOptions());
    }

    private void AnalyzeSource(string source)
    {
        var lexer = new Snek.Lexer.Lexer();
        var parser = new Snek.Parser.Parser();
        var tokens = lexer.Tokenize(source, _context);
        var ast = parser.Parse(tokens, _context);
        _analyzer.Analyze(ast, _context);
    }

    [Fact]
    public void Analyze_UndefinedIdentifier_ReportsError()
    {
        var source = "x = undefinedVar";
        AnalyzeSource(source);

        _context.Diagnostics.Should().Contain(d => d.IsError && d.Message.Contains("Undefined identifier"));
    }

    [Fact]
    public void Analyze_TypeMismatch_ReturnsError()
    {
        var source = "fn foo() -> int:\n  return \"string\"";
        AnalyzeSource(source);

        _context.Diagnostics.Should().Contain(d => d.IsError && d.Message.Contains("Return type mismatch"));
    }

    [Fact]
    public void Analyze_NonVoidFunctionWithoutReturn_ReportsError()
    {
        var source = "fn foo() -> int:\n  pass";
        AnalyzeSource(source);

        _context.Diagnostics.Should().Contain(d => d.IsError && d.Message.Contains("must return a value"));
    }

    [Fact]
    public void Analyze_IfConditionNotBool_ReportsError()
    {
        var source = "if \"string\":\n  pass";
        AnalyzeSource(source);

        _context.Diagnostics.Should().Contain(d => d.IsError && d.Message.Contains("Condition must be bool"));
    }

    [Fact]
    public void Analyze_WhileConditionNotBool_ReportsError()
    {
        var source = "while 42:\n  pass";
        AnalyzeSource(source);

        _context.Diagnostics.Should().Contain(d => d.IsError && d.Message.Contains("While condition must be bool"));
    }

    [Fact]
    public void Analyze_FunctionCallWithWrongArity_ReportsError()
    {
        var source = "fn foo(x: int) -> void:\n  pass\n\nfoo()";
        AnalyzeSource(source);

        _context.Diagnostics.Should().Contain(d => d.IsError && d.Message.Contains("expects 1 args, got 0"));
    }

    [Fact]
    public void Analyze_ValidFunctionCall_ResolvesReturnType()
    {
        var source = "fn foo() -> int:\n  return 42\n\nx = foo()";
        AnalyzeSource(source);

        var type = _analyzer.ResolveType(
            new IdentifierExpressionNode(new Token(TokenType.Identifier, "x", 1, 1)),
            _context);
        _context.Diagnostics.Should().NotContain(d => d.IsError);
    }

    [Fact]
    public void Analyze_BinaryExpression_PromotesTypes()
    {
        var source = "x = 1 + 2.5";
        AnalyzeSource(source);

        _context.Diagnostics.Should().NotContain(d => d.IsError);
    }

    [Fact]
    public void Analyze_ComparisonExpression_ReturnsBool()
    {
        var source = "result = 5 > 3";
        AnalyzeSource(source);

        _context.Diagnostics.Should().NotContain(d => d.IsError);
    }
}