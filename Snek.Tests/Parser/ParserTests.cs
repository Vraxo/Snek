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

        ProgramNode program = Assert.IsType<ProgramNode>(ast);
        FunctionDefNode func = Assert.Single(program.Statements.OfType<FunctionDefNode>());
        Assert.Equal("main", func.Name.Value);
        Assert.Equal("void", func.ReturnType?.Name.Value);
    }

    [Fact]
    public void Parse_IfStatement_CreatesIfStatementNode()
    {
        string source = """
            if true:
              pass
            """;

        AstNode ast = ParseSource(source);

        ProgramNode program = Assert.IsType<ProgramNode>(ast);
        IfStatementNode ifStmt = Assert.Single(program.Statements.OfType<IfStatementNode>());
        Assert.IsType<LiteralExpressionNode>(ifStmt.Condition);
    }

    [Fact]
    public void Parse_WhileStatement_CreatesWhileStatementNode()
    {
        string source = """
            while x < 10:
              x = x + 1
            """;

        AstNode ast = ParseSource(source);

        ProgramNode program = Assert.IsType<ProgramNode>(ast);
        WhileStatementNode whileStmt = Assert.Single(program.Statements.OfType<WhileStatementNode>());
        Assert.IsType<BinaryExpressionNode>(whileStmt.Condition);
    }

    [Fact]
    public void Parse_ReturnStatement_CreatesReturnStatementNode()
    {
        string source = """
            fn foo() -> int:
              return 42
            """;

        AstNode ast = ParseSource(source);

        ProgramNode program = Assert.IsType<ProgramNode>(ast);
        FunctionDefNode func = Assert.Single(program.Statements.OfType<FunctionDefNode>());
        ReturnStatementNode returnStmt = Assert.Single(func.Body.OfType<ReturnStatementNode>());
        Assert.IsType<LiteralExpressionNode>(returnStmt.Value);
    }

    [Fact]
    public void Parse_CallExpression_CreatesCallExpressionNode()
    {
        string source = "print(\"hello\")";
        AstNode ast = ParseSource(source);

        ProgramNode program = Assert.IsType<ProgramNode>(ast);
        ExpressionStatementNode exprStmt = Assert.Single(program.Statements.OfType<ExpressionStatementNode>());
        CallExpressionNode call = Assert.IsType<CallExpressionNode>(exprStmt.Expression);
        Assert.Equal("print", ((IdentifierExpressionNode)call.Callee).Name.Value);
    }

    [Fact]
    public void Parse_BinaryExpression_CreatesBinaryExpressionNode()
    {
        string source = "x + y";
        AstNode ast = ParseSource(source);

        ProgramNode program = Assert.IsType<ProgramNode>(ast);
        ExpressionStatementNode exprStmt = Assert.Single(program.Statements.OfType<ExpressionStatementNode>());
        BinaryExpressionNode binary = Assert.IsType<BinaryExpressionNode>(exprStmt.Expression);
        Assert.Equal(TokenType.Plus, binary.Operator.Type);
    }

    [Fact]
    public void Parse_InvalidSyntax_ReportsError()
    {
        string source = "fn invalid(:";
        AstNode ast = ParseSource(source);

        Assert.Contains(_context.Diagnostics, d => d.IsError);
    }

    [Fact]
    public void Parse_ParameterWithTypeAnnotation_ParsesCorrectly()
    {
        string source = "fn foo(x: int) -> void:\n  pass";
        AstNode ast = ParseSource(source);

        ProgramNode program = Assert.IsType<ProgramNode>(ast);
        FunctionDefNode func = Assert.Single(program.Statements.OfType<FunctionDefNode>());
        ParameterNode param = Assert.Single(func.Parameters);
        Assert.Equal("x", param.Name.Value);
        Assert.Equal("int", param.TypeAnnotation?.Name.Value);
    }
}