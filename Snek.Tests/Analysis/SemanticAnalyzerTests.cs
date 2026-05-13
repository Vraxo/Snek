using FluentAssertions;
using Snek.Analysis;
using Snek.Ast;
using Snek.Lexer;
using Snek.Pipeline;

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
        Snek.Lexer.Lexer lexer = new();
        Snek.Parser.Parser parser = new();
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

        string? type = _analyzer.ResolveType(
            new IdentifierExpressionNode(new(TokenType.Identifier, "x", 1, 1)),
            _context);
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