using FluentAssertions;
using Snek.Analysis;
using Snek.Ast;
using Snek.Generation;
using Snek.Lexer;
using Snek.Pipeline;

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
        Snek.Lexer.Lexer lexer = new();
        Snek.Parser.Parser parser = new();
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
}