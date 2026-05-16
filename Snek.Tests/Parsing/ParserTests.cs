using FluentAssertions;
using Snek.Ast;
using Snek.Lexing;
using Snek.Parsing;
using Snek.Pipeline;

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