using Snek.Ast;
using Snek.Lexer;

namespace Snek.Parser;

public class ExpressionParser
{
    private readonly ParserStream _stream;

    public ExpressionParser(ParserStream stream)
    {
        _stream = stream;
    }

    public ExpressionNode ParseExpression(int precedence = 0)
    {
        var left = ParsePrimary();

        while (true)
        {
            var op = _stream.Current;
            int nextPrecedence = GetPrecedence(op.Type);
            if (nextPrecedence < precedence)
            {
                break;
            }

            _stream.Advance();
            var right = ParseExpression(nextPrecedence + 1);
            left = new BinaryExpressionNode(left, op, right);
        }

        return left;
    }

    private ExpressionNode ParsePrimary()
    {
        if (_stream.Match(TokenType.Identifier))
        {
            var name = _stream.Previous;
            if (_stream.Match(TokenType.LeftParen))
            {
                return ParseCall(name);
            }

            if (_stream.Match(TokenType.Dot))
            {
                return ParseMemberAccess(name);
            }

            return _stream.Match(TokenType.LeftBracket) ? ParseIndex(name) : (ExpressionNode)new IdentifierExpressionNode(name);
        }

        if (_stream.Match(TokenType.StringLiteral) ||
            _stream.Match(TokenType.IntegerLiteral) ||
            _stream.Match(TokenType.FloatLiteral))
        {
            return new LiteralExpressionNode(_stream.Previous);
        }

        if (_stream.Match(TokenType.KeywordTrue) ||
            _stream.Match(TokenType.KeywordFalse) ||
            _stream.Match(TokenType.KeywordNone))
        {
            return new LiteralExpressionNode(_stream.Previous);
        }

        if (_stream.Match(TokenType.LeftParen))
        {
            var expr = ParseExpression();
            _ = _stream.Consume(TokenType.RightParen);
            return expr;
        }

        if (_stream.Match(TokenType.Minus) || _stream.Match(TokenType.KeywordNot))
        {
            var op = _stream.Previous;
            var operand = ParsePrimary();
            return new UnaryExpressionNode(op, operand);
        }

        if (_stream.Match(TokenType.LeftBracket))
        {
            return ParseListLiteral();
        }

        _stream.ReportError($"Unexpected token in expression: '{_stream.Current.Type}'", _stream.Current);
        _stream.Advance();
        return new LiteralExpressionNode(new Token(TokenType.Unknown, "unknown", -1, -1));
    }

    private CallExpressionNode ParseCall(Token callee)
    {
        var args = new List<ExpressionNode>();
        if (!_stream.Match(TokenType.RightParen))
        {
            do
            {
                args.Add(ParseExpression());
            } while (_stream.Match(TokenType.Comma));
            _ = _stream.Consume(TokenType.RightParen);
        }
        return new CallExpressionNode(new IdentifierExpressionNode(callee), args);
    }

    private MemberAccessExpressionNode ParseMemberAccess(Token obj)
    {
        var member = _stream.Consume(TokenType.Identifier);
        return new MemberAccessExpressionNode(new IdentifierExpressionNode(obj), member);
    }

    private IndexExpressionNode ParseIndex(Token target)
    {
        var index = ParseExpression();
        _ = _stream.Consume(TokenType.RightBracket);
        return new IndexExpressionNode(new IdentifierExpressionNode(target), index);
    }

    private ListExpressionNode ParseListLiteral()
    {
        var elements = new List<ExpressionNode>();
        if (!_stream.Match(TokenType.RightBracket))
        {
            do
            {
                elements.Add(ParseExpression());
            } while (_stream.Match(TokenType.Comma));
            _ = _stream.Consume(TokenType.RightBracket);
        }
        return new ListExpressionNode(elements);
    }

    private int GetPrecedence(TokenType type)
    {
        return type switch
        {
            TokenType.KeywordOr => 1,
            TokenType.KeywordAnd => 2,
            TokenType.DoubleEquals or TokenType.NotEquals or TokenType.LessThan or TokenType.GreaterThan
                or TokenType.LessEqual or TokenType.GreaterEqual => 3,
            TokenType.Plus or TokenType.Minus => 4,
            TokenType.Star or TokenType.Slash or TokenType.Percent => 5,
            TokenType.DoubleStar => 6,
            _ => -1
        };
    }
}