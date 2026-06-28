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
        string source = "pass;";

        string output = GenerateSource(source);

        output.Should().Contain("format PE console");
        output.Should().Contain("entry start");
        output.Should().Contain("section '.text' code readable executable");
    }

    [Fact]
    public void Generate_StringLiteral_EmitsDataSection()
    {
        string source = "print(\"hello\");";
        string output = GenerateSource(source);

        output.Should().Contain("section '.data'");
        output.Should().Contain("hello");
    }

    [Fact]
    public void Generate_FunctionCall_EmitsCallInstruction()
    {
        string source = "print(\"test\");";
        string output = GenerateSource(source);

        output.Should().Contain("call [printf]");
    }

    [Fact]
    public void Generate_IntegerLiteral_PushesValue()
    {
        string source = "42;";
        string output = GenerateSource(source);

        output.Should().Contain("push 42");
    }

    [Fact]
    public void Generate_BinaryAddition_EmitsAddInstruction()
    {
        string source = "1 + 2;";
        string output = GenerateSource(source);

        output.Should().Contain("add eax, ebx");
    }

    [Fact]
    public void Generate_IfStatement_EmitsConditionalJump()
    {
        string source = """
            if true {
              x = 1;
            }
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
            while x < 10 {
              x = x + 1;
            }
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
            fn foo() -> int {
              return 42;
            }
            """;

        string output = GenerateSource(source);

        output.Should().Contain("leave");
        output.Should().Contain("ret");
    }

    [Fact]
    public void Generate_ExternalFunction_DeclaredInImportSection()
    {
        string source = "customFunc();";

        string output = GenerateSource(source);

        output.Should().Contain("section '.idata'");
        output.Should().Contain("customFunc");
    }

    [Fact]
    public void DeclareAndUseVariable_ShouldStoreAndLoadValue()
    {
        string source = """
            x: i32 = 42;
            print(x);
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
            msg: str = "Hello";
            print(msg);
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
            a: i32 = 1;
            b: i32 = 2;
            c: i32 = a + b;
            """;

        string output = GenerateSource(source);

        output.Should().Contain("[ebp-4]");  // a
        output.Should().Contain("[ebp-8]");  // b
        output.Should().Contain("[ebp-12]"); // c
    }

    [Fact]
    public void Generator_VariableWithoutInitializer_ShouldDefaultToZero()
    {
        string source = "x: i32;";

        string output = GenerateSource(source);

        output.Should().Contain("xor eax, eax");
        output.Should().Contain("mov [ebp-4], eax");
    }

    [Fact]
    public void Generate_TypeMismatch_ShouldReportError()
    {
        string source = "x: i32 = \"hello\";";
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
        string source = "print(undefinedVar);";
        Lexer lexer = new();
        Parser parser = new();
        SemanticAnalyzer analyzer = new();
        CompilationContext context = new("test.snek", new());

        IEnumerable<Token> tokens = lexer.Tokenize(source, context);
        AstNode ast = parser.Parse(tokens, context);
        analyzer.Analyze(ast, context);

        context.Diagnostics.Should().Contain(d => d.Message.Contains("Undefined identifier"));
    }

    [Fact]
    public void Generate_VariableAssignment_UpdatesValue()
    {
        string source = """
            x: i32 = 42;
            x = 100;
            """;

        string output = GenerateSource(source);

        output.Should().Contain("mov [ebp-4], eax"); // initial store
        output.Should().Contain("push 100");         // load 100
        output.Should().Contain("pop eax");          // prepare 100
    }

    [Fact]
    public void Generate_BinaryComparison_EmitsSetlInstruction()
    {
        string source = "1 < 2;";
        string output = GenerateSource(source);

        output.Should().Contain("cmp eax, ebx");
        output.Should().Contain("setl al");
    }

    [Fact]
    public void Generate_BinaryDivision_EmitsIdivInstruction()
    {
        string source = "10 / 2;";
        string output = GenerateSource(source);

        output.Should().Contain("cdq");
        output.Should().Contain("idiv ebx");
    }

    [Fact]
    public void Generate_ListProperty_EmitsLengthHeaderAndLookup()
    {
        string source = """
            arr: List<i32> = [10, 20];
            len: i32 = arr.length;
            """;

        string output = GenerateSource(source);

        output.Should().Contain("mov dword [eax], 2"); // Alloc header store length
        output.Should().Contain("mov eax, [eax]");     // Property load header lookup
    }
}