using FluentAssertions;
using Snek.Core.Lexing;
using Snek.Core.Pipeline;

namespace Snek.Tests.Lexing;

public class LexerTests
{
    private readonly Lexer _lexer;
    private readonly CompilationContext _context;

    public LexerTests()
    {
        _lexer = new();
        _context = new("test.snek", new());
    }

    [Fact]
    public void Tokenize_Identifier_ReturnsIdentifierToken()
    {
        List<Token> tokens = [.. _lexer.Tokenize("myVar", _context)];

        tokens.Should().ContainSingle(t => t.Type == TokenType.Identifier);
        tokens.First(t => t.Type == TokenType.Identifier).Value.Should().Be("myVar");
    }

    [Fact]
    public void Tokenize_IntegerLiteral_ReturnsIntegerToken()
    {
        List<Token> tokens = [.. _lexer.Tokenize("42", _context)];

        Token token = tokens.First(t => t.Type == TokenType.IntegerLiteral);
        token.Value.Should().Be("42");
    }

    [Fact]
    public void Tokenize_FloatLiteral_ReturnsFloatToken()
    {
        List<Token> tokens = [.. _lexer.Tokenize("3.14", _context)];

        Token token = tokens.First(t => t.Type == TokenType.FloatLiteral);
        token.Value.Should().Be("3.14");
    }

    [Fact]
    public void Tokenize_StringLiteral_ReturnsStringToken()
    {
        List<Token> tokens = [.. _lexer.Tokenize("\"hello\"", _context)];

        Token token = tokens.First(t => t.Type == TokenType.StringLiteral);
        token.Value.Should().Be("hello");
    }

    [Fact]
    public void Tokenize_Keyword_ReturnsKeywordToken()
    {
        List<Token> tokens = [.. _lexer.Tokenize("fn", _context)];

        tokens.Should().Contain(t => t.Type == TokenType.KeywordFn);
    }

    [Fact]
    public void Tokenize_Operator_ReturnsCorrectOperatorToken()
    {
        List<Token> tokens = [.. _lexer.Tokenize("==", _context)];

        tokens.Should().Contain(t => t.Type == TokenType.DoubleEquals);
    }

    [Fact]
    public void Tokenize_WithComments_IgnoresComments()
    {
        List<Token> tokens = [.. _lexer.Tokenize("x # this is a comment\ny", _context)];

        List<string> identifiers = [.. tokens.Where(t => t.Type == TokenType.Identifier)
            .Select(t => t.Value)];

        identifiers.Should().Contain("x").And.Contain("y");
        tokens.Select(t => t.Value).Should().NotContain("this is a comment");
    }

    [Fact]
    public void Tokenize_WithIndentation_EmitsIndentDedentTokens()
    {
        string source = """
            fn main():
              x = 1
            """;
        List<Token> tokens = [.. _lexer.Tokenize(source, _context)];

        tokens.Should().Contain(t => t.Type == TokenType.Indent);
        tokens.Should().Contain(t => t.Type == TokenType.Dedent);
    }

    [Fact]
    public void Tokenize_UnterminatedString_ReportsError()
    {
        List<Token> tokens = [.. _lexer.Tokenize("\"unterminated", _context)];

        _context.Diagnostics.Should().Contain(d => d.IsError && d.Message.Contains("Unterminated"));
    }

    [Fact]
    public void Tokenize_Eof_ReturnsEofToken()
    {
        List<Token> tokens = [.. _lexer.Tokenize("", _context)];

        tokens.Should().Contain(t => t.Type == TokenType.Eof);
    }
}