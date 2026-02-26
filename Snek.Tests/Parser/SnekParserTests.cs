using Snek.Ast;
using Snek.Lexer;
using Snek.Parser;
using Snek.Pipeline;

namespace Snek.Tests.Parser;

public class SnekParserTests
{
    private readonly SnekParser _parser;
    private readonly CompilationContext _context;

    public SnekParserTests()
    {
        _parser = new SnekParser();
        _context = new CompilationContext("test.snek", new PipelineOptions());
    }

    private AstNode ParseSource(string source)
    {
        var lexer = new SnekLexer();
        var tokens = lexer.Tokenize(source, _context);
        return _parser.Parse(tokens, _context);
    }

    [Fact]
    public void Parse_FunctionDef_CreatesFunctionDefNode()
    {
        var source = "fn main() -> void:\n  pass";
        var ast = ParseSource(source);

        var program = Assert.IsType<ProgramNode>(ast);
        var func = Assert.Single(program.Statements.OfType<FunctionDefNode>());
        Assert.Equal("main", func.Name.Value);
        Assert.Equal("void", func.ReturnType?.Name.Value);
    }

    [Fact]
    public void Parse_IfStatement_CreatesIfStatementNode()
    {
        var source = "if true:\n  pass";
        var ast = ParseSource(source);

        var program = Assert.IsType<ProgramNode>(ast);
        var ifStmt = Assert.Single(program.Statements.OfType<IfStatementNode>());
        _ = Assert.IsType<LiteralExpressionNode>(ifStmt.Condition);
    }

    [Fact]
    public void Parse_WhileStatement_CreatesWhileStatementNode()
    {
        var source = "while x < 10:\n  x = x + 1";
        var ast = ParseSource(source);

        var program = Assert.IsType<ProgramNode>(ast);
        var whileStmt = Assert.Single(program.Statements.OfType<WhileStatementNode>());
        _ = Assert.IsType<BinaryExpressionNode>(whileStmt.Condition);
    }

    [Fact]
    public void Parse_ReturnStatement_CreatesReturnStatementNode()
    {
        var source = "fn foo() -> int:\n  return 42";
        var ast = ParseSource(source);

        var program = Assert.IsType<ProgramNode>(ast);
        var func = Assert.Single(program.Statements.OfType<FunctionDefNode>());
        var returnStmt = Assert.Single(func.Body.OfType<ReturnStatementNode>());
        _ = Assert.IsType<LiteralExpressionNode>(returnStmt.Value);
    }

    [Fact]
    public void Parse_CallExpression_CreatesCallExpressionNode()
    {
        var source = "print(\"hello\")";
        var ast = ParseSource(source);

        var program = Assert.IsType<ProgramNode>(ast);
        var exprStmt = Assert.Single(program.Statements.OfType<ExpressionStatementNode>());
        var call = Assert.IsType<CallExpressionNode>(exprStmt.Expression);
        Assert.Equal("print", ((IdentifierExpressionNode)call.Callee).Name.Value);
    }

    [Fact]
    public void Parse_BinaryExpression_CreatesBinaryExpressionNode()
    {
        var source = "x + y";
        var ast = ParseSource(source);

        var program = Assert.IsType<ProgramNode>(ast);
        var exprStmt = Assert.Single(program.Statements.OfType<ExpressionStatementNode>());
        var binary = Assert.IsType<BinaryExpressionNode>(exprStmt.Expression);
        Assert.Equal(TokenType.Plus, binary.Operator.Type);
    }

    [Fact]
    public void Parse_InvalidSyntax_ReportsError()
    {
        var source = "fn invalid(:";
        var ast = ParseSource(source);

        Assert.Contains(_context.Diagnostics, d => d.IsError);
    }

    [Fact]
    public void Parse_ParameterWithTypeAnnotation_ParsesCorrectly()
    {
        var source = "fn foo(x: int) -> void:\n  pass";
        var ast = ParseSource(source);

        var program = Assert.IsType<ProgramNode>(ast);
        var func = Assert.Single(program.Statements.OfType<FunctionDefNode>());
        var param = Assert.Single(func.Parameters);
        Assert.Equal("x", param.Name.Value);
        Assert.Equal("int", param.TypeAnnotation?.Name.Value);
    }
}