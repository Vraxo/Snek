using Snek.Diagnoistics;
using Snek.Lexer;
using Snek.Pipeline;

namespace Snek.Parser;

public class ParserStream
{
    private readonly IEnumerator<Token> _tokens;
    private readonly CompilationContext _context;

    public Token Current { get; private set; }
    public Token Previous { get; private set; }

    public ParserStream(IEnumerable<Token> tokens, CompilationContext context)
    {
        _tokens = tokens.GetEnumerator();
        _context = context;
        Current = new Token(TokenType.Eof, " ", -1, -1);
        Previous = Current;
        Advance(); // Initialize Current
    }

    public void Advance()
    {
        Previous = Current;
        Current = _tokens.MoveNext() ? _tokens.Current : new Token(TokenType.Eof, " ", -1, -1);
    }

    public bool Match(TokenType type)
    {
        if (Current.Type == type)
        {
            Advance();
            return true;
        }
        return false;
    }

    public Token Consume(TokenType type)
    {
        if (Current.Type == type)
        {
            var token = Current;
            Advance();
            return token;
        }

        ReportError($"Expected '{type}' but got '{Current.Type}'", Current);
        return Current;
    }

    public void ReportError(string message, Token atToken)
    {
        _context.Diagnostics.Add(new Diagnostic(
            _context.SourceName,
            message,
            atToken.Line,
            atToken.Column,
            DiagnosticSeverity.Error));
    }
}