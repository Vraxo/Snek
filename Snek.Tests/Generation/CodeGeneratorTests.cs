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
        string source = """
            fn main() -> void:
              pass
            """;

        string output = GenerateSource(source);

        Assert.Contains("format PE console", output);
        Assert.Contains("entry start", output);
        Assert.Contains("section '.text' code readable executable", output);
    }

    [Fact]
    public void Generate_StringLiteral_EmitsDataSection()
    {
        string source = "print(\"hello\")";
        string output = GenerateSource(source);

        Assert.Contains("section '.data'", output);
        Assert.Contains("hello", output);
    }

    [Fact]
    public void Generate_FunctionCall_EmitsCallInstruction()
    {
        string source = """
            fn main() -> void:
              print("test")
            """;
        string output = GenerateSource(source);

        Assert.Contains("call [printf]", output);
    }

    [Fact]
    public void Generate_IntegerLiteral_PushesValue()
    {
        string source = "x = 42";
        string output = GenerateSource(source);

        Assert.Contains("push 42", output);
    }

    [Fact]
    public void Generate_BinaryAddition_EmitsAddInstruction()
    {
        string source = "result = 1 + 2";
        string output = GenerateSource(source);

        Assert.Contains("add eax, ebx", output);
    }

    [Fact]
    public void Generate_IfStatement_EmitsConditionalJump()
    {
        string source = """
            if true:
              x = 1
            """;

        string output = GenerateSource(source);

        Assert.Contains("jz", output);
        Assert.Contains("_else_", output);
        Assert.Contains("_endif_", output);
    }

    [Fact]
    public void Generate_WhileLoop_EmitsLoopStructure()
    {
        string source = """
            while x < 10:
              x = x + 1
            """;

        string output = GenerateSource(source);

        Assert.Contains("_while_", output);
        Assert.Contains("_endwhile_", output);
        Assert.Contains("jmp", output);
    }

    [Fact]
    public void Generate_ReturnStatement_EmitsReturnSequence()
    {
        string source = """
            fn foo() -> int:
              return 42
            """;

        string output = GenerateSource(source);

        Assert.Contains("leave", output);
        Assert.Contains("ret", output);
    }

    [Fact]
    public void Generate_ExternalFunction_DeclaredInImportSection()
    {
        string source = """
            fn main() -> void:
              customFunc()
            """;

        string output = GenerateSource(source);

        Assert.Contains("section '.idata'", output);
        Assert.Contains("customFunc", output);
    }
}