using Snek.Diagnoistics;
using Snek.Pipeline;
using System.Text;

namespace Snek.Lexer;

public class Lexer : ILexer
{
    private readonly LexerRules _rules;
    private string _source = string.Empty;
    private int _position;
    private int _line = 1;
    private int _column = 1;
    private CompilationContext? _context;
    private readonly Stack<int> _indentStack = new();

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
        _indentStack.Clear();
        _indentStack.Push(0);

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

        // Emit dedents to close all indentation levels
        while (_indentStack.Count > 1)
        {
            _indentStack.Pop();
            tokens.Add(new Token(TokenType.Dedent, "", _line, _column));
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
        return _position + offset < _source.Length ? _source[_position + offset] : '\0';
    }

    private char Advance()
    {
        char c = _source[_position++];
        if (c == '\n') { _line++; _column = 1; }
        else { _column++; }
        return c;
    }

    private void SkipWhitespaceAndComments()
    {
        while (!IsAtEnd())
        {
            char c = Peek();

            if (char.IsWhiteSpace(c) && c != '\n')
            {
                Advance(); continue;
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

        if (c == '\n')
        {
            Advance();
            HandleNewline(tokens, startLine, startColumn);
            return true;
        }

        // Single-char structural tokens not in operators list
        if (c is '(' or ')' or '[' or ']' or '{' or '}' or ',' or '.' or ':')
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
                _ => TokenType.Unknown
            };
            tokens.Add(new Token(type, c.ToString(), startLine, startColumn));
            return true;
        }

        return false;
    }

    private void HandleNewline(List<Token> tokens, int line, int column)
    {
        if (!_rules.SupportsIndentation)
        {
            tokens.Add(new Token(TokenType.Newline, "", line, column));
            return;
        }

        // Always emit Newline first, before any Indent/Dedent tokens.
        // This ensures the parser sees "Statement -> Newline -> Indent -> Block".
        tokens.Add(new Token(TokenType.Newline, "", line, column));

        // Calculate indentation of next non-empty line
        int indent = 0;
        int tempPos = _position;
        while (tempPos < _source.Length)
        {
            char c = _source[tempPos];
            if (c == ' ') { indent++; tempPos++; }
            else if (c == '\t') { indent += _rules.TabWidth; tempPos++; }
            else if (c == '\n') { indent = 0; tempPos++; }
            else if (c == '#')
            {
                while (tempPos < _source.Length && _source[tempPos] != '\n')
                {
                    tempPos++;
                }
            }
            else
            {
                break;
            }
        }

        int currentIndent = _indentStack.Peek();

        if (indent > currentIndent)
        {
            _indentStack.Push(indent);
            tokens.Add(new(TokenType.Indent, "", line, column));
        }
        else if (indent < currentIndent)
        {
            while (_indentStack.Count > 1 && _indentStack.Peek() > indent)
            {
                _indentStack.Pop();
                tokens.Add(new(TokenType.Dedent, "", line, column));
            }
            if (_indentStack.Peek() != indent)
            {
                ReportError("Inconsistent indentation", line, column);
            }
        }
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