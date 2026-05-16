using Snek.Diagnoistics;
using Snek.Lexer;
using Snek.Pipeline;

namespace Snek.Parser;

public class ParserStream
{
    private readonly List<Token> _tokens;
    private readonly CompilationContext _context;
    private int _position;

    public Token Current { get; private set; }
    public Token Previous { get; private set; }

    public ParserStream(IEnumerable<Token> tokens, CompilationContext context)
    {
        _tokens = [.. tokens];
        _context = context;
        _position = 0;

        Current = _tokens.Count > 0
            ? _tokens[0]
            : new(TokenType.Eof, " ", -1, -1);

        Previous = Current;
    }

    public void Advance()
    {
        Previous = Current;
        _position++;

        Current = _position < _tokens.Count
            ? _tokens[_position]
            : new(TokenType.Eof, " ", -1, -1);
    }

    public Token Peek(int offset = 1)
    {
        int index = _position + offset;

        if (index >= 0 && index < _tokens.Count)
        {
            return _tokens[index];
        }

        return new(TokenType.Eof, "", -1, -1);
    }

    public bool Match(TokenType type)
    {
        if (Current.Type != type)
        {
            return false;
        }

        Advance();
        return true;
    }

    public Token Consume(TokenType type)
    {
        if (Current.Type != type)
        {
            ReportError($"Expected '{type}' but got '{Current.Value}'", Current);
            return Current;
        }

        Token token = Current;
        Advance();
        return token;
    }

    public void ReportError(string message, Token atToken)
    {
        _context.Diagnostics.Add(new(
            _context.SourceName,
            message,
            atToken.Line,
            atToken.Column,
            DiagnosticSeverity.Error,
            atToken.Value.Length));
    }
}