using Snek.Core.Diagnoistics;
using Snek.Core.Pipeline;
using System.Text;

namespace Snek.Core.Lexing;

public class Lexer : ILexer
{
    private readonly LexerRules _rules;
    private readonly List<(string Pattern, TokenType Type)> _orderedOperators;
    private string _source = string.Empty;
    private int _position;
    private int _line = 1;
    private int _column = 1;
    private CompilationContext? _context;

    public Lexer(LexerRules? rules = null)
    {
        _rules = rules ?? new();
        _orderedOperators = _rules.Operators.OrderByDescending(o => o.Pattern.Length).ToList();
    }

    public IEnumerable<Token> Tokenize(string source, CompilationContext context)
    {
        _source = source;
        _position = 0;
        _line = 1;
        _column = 1;
        _context = context;

        List<Token> tokens = [];

        while (!IsAtEnd())
        {
            SkipWhitespaceAndComments();
            if (IsAtEnd()) break;

            int startLine = _line;
            int startColumn = _column;

            if (TryReadKeywordOrIdentifier(tokens) ||
                TryReadNumber(tokens) ||
                TryReadString(tokens) ||
                TryReadOperator(tokens))
            {
                continue;
            }

            ReportError($"Unexpected character '{Peek()}'", startLine, startColumn);
            Advance();
        }

        tokens.Add(new Token(TokenType.Eof, "", _line, _column));
        return tokens;
    }

    private bool IsAtEnd() => _position >= _source.Length;

    private char Peek(int offset = 0) =>
        _position + offset < _source.Length ? _source[_position + offset] : '\0';

    private char Advance()
    {
        char c = _source[_position++];
        if (c == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }
        return c;
    }

    private void SkipWhitespaceAndComments()
    {
        while (!IsAtEnd())
        {
            char c = Peek();
            if (char.IsWhiteSpace(c))
            {
                Advance();
            }
            else if (c == '#')
            {
                while (!IsAtEnd() && Peek() != '\n') Advance();
            }
            else
            {
                break;
            }
        }
    }

    private bool TryReadKeywordOrIdentifier(List<Token> tokens)
    {
        char first = Peek();
        if (!char.IsLetter(first) && first != '_' && !_rules.IdentifierStartChars.Contains(first))
        {
            return false;
        }

        int startLine = _line, startColumn = _column, startPos = _position;
        while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '_' || _rules.IdentifierContinueChars.Contains(Peek())))
        {
            Advance();
        }

        string value = _source[startPos.._position];
        TokenType type = _rules.Keywords.TryGetValue(value, out TokenType keywordType) ? keywordType : TokenType.Identifier;
        tokens.Add(new Token(type, value, startLine, startColumn));
        return true;
    }

    private bool TryReadNumber(List<Token> tokens)
    {
        if (!char.IsDigit(Peek())) return false;

        int startLine = _line, startColumn = _column, startPos = _position;
        bool isFloat = false;

        while (char.IsDigit(Peek())) Advance();

        if (Peek() == '.' && char.IsDigit(Peek(1)))
        {
            isFloat = true;
            Advance(); // consume .
            while (char.IsDigit(Peek())) Advance();
        }

        if (Peek() is 'e' or 'E')
        {
            isFloat = true;
            Advance(); // consume e/E
            if (Peek() is '+' or '-') Advance();
            while (char.IsDigit(Peek())) Advance();
        }

        string value = _source[startPos.._position];
        tokens.Add(new Token(isFloat ? TokenType.FloatLiteral : TokenType.IntegerLiteral, value, startLine, startColumn));
        return true;
    }

    private bool TryReadString(List<Token> tokens)
    {
        char delimiter = Peek();
        if (delimiter != _rules.StringDelimiter && delimiter != _rules.CharDelimiter) return false;

        int startLine = _line, startColumn = _column;
        Advance(); // consume opening delimiter

        StringBuilder sb = new();
        while (!IsAtEnd() && Peek() != delimiter)
        {
            char ch = Advance();
            if (ch == '\\' && !IsAtEnd())
            {
                char escaped = Advance();
                sb.Append(escaped switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    '\\' => '\\',
                    '"' => '"',
                    '\'' => '\'',
                    _ => escaped
                });
            }
            else
            {
                sb.Append(ch);
            }
        }

        if (IsAtEnd())
        {
            ReportError("Unterminated string literal", startLine, startColumn);
            return true;
        }
        Advance(); // consume closing delimiter

        TokenType type = delimiter == _rules.CharDelimiter ? TokenType.CharLiteral : TokenType.StringLiteral;
        tokens.Add(new Token(type, sb.ToString(), startLine, startColumn));
        return true;
    }

    private bool TryReadOperator(List<Token> tokens)
    {
        ReadOnlySpan<char> span = _source.AsSpan(_position);
        foreach (var (pattern, type) in _orderedOperators)
        {
            if (span.StartsWith(pattern))
            {
                int startLine = _line, startColumn = _column;
                _position += pattern.Length;
                _column += pattern.Length;
                tokens.Add(new Token(type, pattern, startLine, startColumn));
                return true;
            }
        }
        return false;
    }

    private void ReportError(string message, int line, int column)
    {
        _context?.Diagnostics.Add(new(
            _context.SourceName,
            message,
            line,
            column,
            DiagnosticSeverity.Error));
    }
}