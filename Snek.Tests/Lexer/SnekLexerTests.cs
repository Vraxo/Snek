using Snek.Lexer;
using Snek.Pipeline;

namespace Snek.Tests.Lexer;

public class SnekLexerTests
{
    private readonly SnekLexer _lexer;
    private readonly CompilationContext _context;

    public SnekLexerTests()
    {
        _lexer = new SnekLexer();
        _context = new CompilationContext("test.snek", new PipelineOptions());
    }

    [Fact]
    public void Tokenize_Identifier_ReturnsIdentifierToken()
    {
        var tokens = _lexer.Tokenize("myVar", _context).ToList();

        _ = Assert.Single(tokens, t => t.Type == TokenType.Identifier);
        Assert.Equal("myVar", tokens.First(t => t.Type == TokenType.Identifier).Value);
    }

    [Fact]
    public void Tokenize_IntegerLiteral_ReturnsIntegerToken()
    {
        var tokens = _lexer.Tokenize("42", _context).ToList();

        var token = tokens.First(t => t.Type == TokenType.IntegerLiteral);
        Assert.Equal("42", token.Value);
    }

    [Fact]
    public void Tokenize_FloatLiteral_ReturnsFloatToken()
    {
        var tokens = _lexer.Tokenize("3.14", _context).ToList();

        var token = tokens.First(t => t.Type == TokenType.FloatLiteral);
        Assert.Equal("3.14", token.Value);
    }

    [Fact]
    public void Tokenize_StringLiteral_ReturnsStringToken()
    {
        var tokens = _lexer.Tokenize("\"hello\"", _context).ToList();

        var token = tokens.First(t => t.Type == TokenType.StringLiteral);
        Assert.Equal("hello", token.Value);
    }

    [Fact]
    public void Tokenize_Keyword_ReturnsKeywordToken()
    {
        var tokens = _lexer.Tokenize("fn", _context).ToList();

        Assert.Contains(tokens, t => t.Type == TokenType.KeywordFn);
    }

    [Fact]
    public void Tokenize_Operator_ReturnsCorrectOperatorToken()
    {
        var tokens = _lexer.Tokenize("==", _context).ToList();

        Assert.Contains(tokens, t => t.Type == TokenType.DoubleEquals);
    }

    [Fact]
    public void Tokenize_WithComments_IgnoresComments()
    {
        var tokens = _lexer.Tokenize("x # this is a comment\ny", _context).ToList();

        var identifiers = tokens.Where(t => t.Type == TokenType.Identifier).Select(t => t.Value).ToList();
        Assert.Contains("x", identifiers);
        Assert.Contains("y", identifiers);
        Assert.DoesNotContain("this is a comment", tokens.Select(t => t.Value));
    }

    [Fact]
    public void Tokenize_WithIndentation_EmitsIndentDedentTokens()
    {
        var source = "fn main():\n  x = 1";
        var tokens = _lexer.Tokenize(source, _context).ToList();

        Assert.Contains(tokens, t => t.Type == TokenType.Indent);
        Assert.Contains(tokens, t => t.Type == TokenType.Dedent);
    }

    [Fact]
    public void Tokenize_UnterminatedString_ReportsError()
    {
        var tokens = _lexer.Tokenize("\"unterminated", _context).ToList();

        Assert.Contains(_context.Diagnostics, d => d.IsError && d.Message.Contains("Unterminated"));
    }

    [Fact]
    public void Tokenize_Eof_ReturnsEofToken()
    {
        var tokens = _lexer.Tokenize("", _context).ToList();

        Assert.Contains(tokens, t => t.Type == TokenType.Eof);
    }
}