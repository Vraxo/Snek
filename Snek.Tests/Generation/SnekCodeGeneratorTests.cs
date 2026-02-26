using Snek.Analysis;
using Snek.Generation;
using Snek.Lexer;
using Snek.Parser;
using Snek.Pipeline;

namespace Snek.Tests.Generation;

public class SnekCodeGeneratorTests
{
    private readonly SnekCodeGenerator _generator;
    private readonly CompilationContext _context;

    public SnekCodeGeneratorTests()
    {
        _generator = new SnekCodeGenerator();
        _context = new CompilationContext("test.snek", new PipelineOptions());
    }

    private string GenerateSource(string source)
    {
        var lexer = new SnekLexer();
        var parser = new SnekParser();
        var analyzer = new SnekSemanticAnalyzer();

        var tokens = lexer.Tokenize(source, _context);
        var ast = parser.Parse(tokens, _context);
        analyzer.Analyze(ast, _context);

        return _generator.Generate(ast, _context) ?? string.Empty;
    }

    [Fact]
    public void Generate_EmptyProgram_ProducesValidHeader()
    {
        var source = "fn main() -> void:\n  pass";
        var output = GenerateSource(source);

        Assert.Contains("format PE console", output);
        Assert.Contains("entry start", output);
        Assert.Contains("section '.text' code readable executable", output);
    }

    [Fact]
    public void Generate_StringLiteral_EmitsDataSection()
    {
        var source = "print(\"hello\")";
        var output = GenerateSource(source);

        Assert.Contains("section '.data'", output);
        Assert.Contains("hello", output);
    }

    [Fact]
    public void Generate_FunctionCall_EmitsCallInstruction()
    {
        var source = "fn main() -> void:\n  print(\"test\")";
        var output = GenerateSource(source);

        Assert.Contains("call [printf]", output);
    }

    [Fact]
    public void Generate_IntegerLiteral_PushesValue()
    {
        var source = "x = 42";
        var output = GenerateSource(source);

        Assert.Contains("push 42", output);
    }

    [Fact]
    public void Generate_BinaryAddition_EmitsAddInstruction()
    {
        var source = "result = 1 + 2";
        var output = GenerateSource(source);

        Assert.Contains("add eax, ebx", output);
    }

    [Fact]
    public void Generate_IfStatement_EmitsConditionalJump()
    {
        var source = "if true:\n  x = 1";
        var output = GenerateSource(source);

        Assert.Contains("jz", output);
        Assert.Contains("_else_", output);
        Assert.Contains("_endif_", output);
    }

    [Fact]
    public void Generate_WhileLoop_EmitsLoopStructure()
    {
        var source = "while x < 10:\n  x = x + 1";
        var output = GenerateSource(source);

        Assert.Contains("_while_", output);
        Assert.Contains("_endwhile_", output);
        Assert.Contains("jmp", output);
    }

    [Fact]
    public void Generate_ReturnStatement_EmitsReturnSequence()
    {
        var source = "fn foo() -> int:\n  return 42";
        var output = GenerateSource(source);

        Assert.Contains("leave", output);
        Assert.Contains("ret", output);
    }

    [Fact]
    public void Generate_ExternalFunction_DeclaredInImportSection()
    {
        var source = "fn main() -> void:\n  customFunc()";
        var output = GenerateSource(source);

        Assert.Contains("section '.idata'", output);
        Assert.Contains("customFunc", output);
    }
}