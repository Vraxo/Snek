using FluentAssertions;
using Snek.Ast;
using Snek.Lexer;
using Snek.Pipeline;

namespace Snek.Tests.Parser;

public class ParserTests
{
    private readonly Snek.Parser.Parser _parser;
    private readonly CompilationContext _context;

    public ParserTests()
    {
        _parser = new Snek.Parser.Parser();
        _context = new("test.snek", new());
    }

    private AstNode ParseSource(string source)
    {
        Snek.Lexer.Lexer lexer = new();
        IEnumerable<Token> tokens = lexer.Tokenize(source, _context);
        return _parser.Parse(tokens, _context);
    }

    [Fact]
    public void Parse_FunctionDef_CreatesFunctionDefNode()
    {
        string source = """
            fn main() -> void:
              pass
            """;

        AstNode ast = ParseSource(source);

        ast.Should().BeOfType<ProgramNode>();
        var program = (ProgramNode)ast;
        var func = program.Statements.OfType<FunctionDefNode>().Should().ContainSingle().Subject;
        func.Name.Value.Should().Be("main");
        func.ReturnType?.Name.Value.Should().Be("void");
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
        var program = (ProgramNode)ast;
        var ifStmt = program.Statements.OfType<IfStatementNode>().Should().ContainSingle().Subject;
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
        var program = (ProgramNode)ast;
        var whileStmt = program.Statements.OfType<WhileStatementNode>().Should().ContainSingle().Subject;
        whileStmt.Condition.Should().BeOfType<BinaryExpressionNode>();
    }

    [Fact]
    public void Parse_ReturnStatement_CreatesReturnStatementNode()
    {
        string source = """
            fn foo() -> int:
              return 42
            """;

        AstNode ast = ParseSource(source);

        ast.Should().BeOfType<ProgramNode>();
        var program = (ProgramNode)ast;
        var func = program.Statements.OfType<FunctionDefNode>().Should().ContainSingle().Subject;
        var returnStmt = func.Body.OfType<ReturnStatementNode>().Should().ContainSingle().Subject;
        returnStmt.Value.Should().BeOfType<LiteralExpressionNode>();
    }

    [Fact]
    public void Parse_CallExpression_CreatesCallExpressionNode()
    {
        string source = "print(\"hello\")";
        AstNode ast = ParseSource(source);

        ast.Should().BeOfType<ProgramNode>();
        var program = (ProgramNode)ast;
        var exprStmt = program.Statements.OfType<ExpressionStatementNode>().Should().ContainSingle().Subject;
        var call = exprStmt.Expression.Should().BeOfType<CallExpressionNode>().Subject;
        ((IdentifierExpressionNode)call.Callee).Name.Value.Should().Be("print");
    }

    [Fact]
    public void Parse_BinaryExpression_CreatesBinaryExpressionNode()
    {
        string source = "x + y";
        AstNode ast = ParseSource(source);

        ast.Should().BeOfType<ProgramNode>();
        var program = (ProgramNode)ast;
        var exprStmt = program.Statements.OfType<ExpressionStatementNode>().Should().ContainSingle().Subject;
        var binary = exprStmt.Expression.Should().BeOfType<BinaryExpressionNode>().Subject;
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
        string source = "fn foo(x: int) -> void:\n  pass";
        AstNode ast = ParseSource(source);

        ast.Should().BeOfType<ProgramNode>();
        var program = (ProgramNode)ast;
        var func = program.Statements.OfType<FunctionDefNode>().Should().ContainSingle().Subject;
        var param = func.Parameters.Should().ContainSingle().Subject;
        param.Name.Value.Should().Be("x");
        param.TypeAnnotation?.Name.Value.Should().Be("int");
    }
}