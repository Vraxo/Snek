using Snek.Core.Diagnoistics;
using Snek.Core.Pipeline;
using System.Text;

namespace Snek.Core.Lexing;

public class Lexer : ILexer
{
    private readonly LexerRules _rules;
    private string _source = string.Empty;
    private int _position;
    private int _line = 1;
    private int _column = 1;
    private CompilationContext? _context;

    public Lexer(LexerRules? rules = null)
    {
        _rules = rules ?? new();
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
            if (IsAtEnd())
            {
                break;
            }

            int startLine = _line;
            int startColumn = _column;

            if (TryReadKeywordOrIdentifier(tokens))
            {
                continue;
            }

            if (TryReadNumber(tokens))
            {
                continue;
            }

            if (TryReadString(tokens))
            {
                continue;
            }

            if (TryReadOperator(tokens))
            {
                continue;
            }

            if (TryReadStructural(tokens))
            {
                continue;
            }

            ReportError($"Unexpected character '{Peek()}'", startLine, startColumn);
            Advance();
        }

        tokens.Add(new Token(TokenType.Eof, "", _line, _column));
        return tokens;
    }

    private bool IsAtEnd()
    {
        return _position >= _source.Length;
    }

    private char Peek(int offset = 0)
    {
        return _position + offset < _source.Length
            ? _source[_position + offset]
            : '\0';
    }

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
                continue;
            }

            if (c == '#')
            {
                while (!IsAtEnd() && Peek() != '\n')
                {
                    Advance();
                }
                continue;
            }

            break;
        }
    }

    private bool TryReadKeywordOrIdentifier(List<Token> tokens)
    {
        if (!char.IsLetter(Peek()) && Peek() != '_' && !_rules.IdentifierStartChars.Contains(Peek()))
        {
            return false;
        }

        int startLine = _line;
        int startColumn = _column;
        StringBuilder sb = new();

        while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '_' || _rules.IdentifierContinueChars.Contains(Peek())))
        {
            sb.Append(Advance());
        }

        string value = sb.ToString();
        TokenType type = _rules.Keywords.TryGetValue(value, out TokenType keywordType) ? keywordType : TokenType.Identifier;
        tokens.Add(new Token(type, value, startLine, startColumn));
        return true;
    }

    private bool TryReadNumber(List<Token> tokens)
    {
        if (!char.IsDigit(Peek()))
        {
            return false;
        }

        int startLine = _line;
        int startColumn = _column;
        StringBuilder sb = new();
        bool isFloat = false;

        // Integer part
        while (char.IsDigit(Peek()))
        {
            sb.Append(Advance());
        }

        // Fractional part
        if (Peek() == '.' && char.IsDigit(Peek(1)))
        {
            isFloat = true;
            sb.Append(Advance()); // .
            while (char.IsDigit(Peek()))
            {
                sb.Append(Advance());
            }
        }

        // Exponent
        if (Peek() is 'e' or 'E')
        {
            isFloat = true;
            sb.Append(Advance());
            if (Peek() is '+' or '-')
            {
                sb.Append(Advance());
            }

            while (char.IsDigit(Peek()))
            {
                sb.Append(Advance());
            }
        }

        string value = sb.ToString();
        TokenType type = isFloat ? TokenType.FloatLiteral : TokenType.IntegerLiteral;
        tokens.Add(new Token(type, value, startLine, startColumn));
        return true;
    }

    private bool TryReadString(List<Token> tokens)
    {
        char c = Peek();
        if (c != _rules.StringDelimiter && c != _rules.CharDelimiter)
        {
            return false;
        }

        int startLine = _line;
        int startColumn = _column;
        char delimiter = Advance(); // consume opening quote
        bool isChar = delimiter == _rules.CharDelimiter;
        StringBuilder sb = new();

        while (!IsAtEnd() && Peek() != delimiter)
        {
            char ch = Advance();

            if (ch == '\\')
            {
                if (IsAtEnd())
                {
                    break;
                }

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

        if (IsAtEnd() || Peek() != delimiter)
        {
            ReportError("Unterminated string literal", startLine, startColumn);
            return true;
        }
        Advance(); // consume closing quote

        TokenType type = isChar ? TokenType.CharLiteral : TokenType.StringLiteral;
        tokens.Add(new Token(type, sb.ToString(), startLine, startColumn));
        return true;
    }

    private bool TryReadOperator(List<Token> tokens)
    {
        // Try longest operators first
        foreach ((string? pattern, TokenType type) in _rules.Operators.OrderByDescending(o => o.Pattern.Length))
        {
            if (!MatchString(pattern))
            {
                continue;
            }

            int startLine = _line;
            int startColumn = _column;
            // Advance past the matched pattern
            for (int i = 0; i < pattern.Length; i++)
            {
                Advance();
            }

            tokens.Add(new Token(type, pattern, startLine, startColumn));
            return true;
        }
        return false;
    }

    private bool TryReadStructural(List<Token> tokens)
    {
        char c = Peek();
        int startLine = _line;
        int startColumn = _column;

        // Single-char structural tokens not in operators list
        if (c is '(' or ')' or '[' or ']' or '{' or '}' or ',' or '.' or ':' or ';')
        {
            Advance();
            TokenType type = c switch
            {
                '(' => TokenType.LeftParen,
                ')' => TokenType.RightParen,
                '[' => TokenType.LeftBracket,
                ']' => TokenType.RightBracket,
                '{' => TokenType.LeftBrace,
                '}' => TokenType.RightBrace,
                ',' => TokenType.Comma,
                '.' => TokenType.Dot,
                ':' => TokenType.Colon,
                ';' => TokenType.Semicolon,
                _ => TokenType.Unknown
            };
            tokens.Add(new Token(type, c.ToString(), startLine, startColumn));
            return true;
        }

        return false;
    }

    private bool MatchString(string expected)
    {
        if (_position + expected.Length > _source.Length)
        {
            return false;
        }

        for (int i = 0; i < expected.Length; i++)
        {
            if (_source[_position + i] == expected[i])
            {
                continue;
            }

            return false;
        }

        return true;
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