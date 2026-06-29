using Snek.Core.Ast;
using Snek.Core.Lexing;

namespace Snek.Core.Parsing;

public class ExpressionParser
{
    private readonly ParserStream _stream;

    public ExpressionParser(ParserStream stream)
    {
        _stream = stream;
    }

    public ExpressionNode ParseExpression(int precedence = 0)
    {
        ExpressionNode left = ParsePrimary();

        while (true)
        {
            Token op = _stream.Current;
            int nextPrecedence = GetPrecedence(op.Type);

            if (nextPrecedence < precedence)
            {
                break;
            }

            _stream.Advance();

            ExpressionNode right = ParseExpression(nextPrecedence + 1);
            left = new BinaryExpressionNode(left, op, right);
        }

        return left;
    }

    private ExpressionNode ParsePrimary()
    {
        ExpressionNode left = ParseBasePrimary();

        while (true)
        {
            if (_stream.Match(TokenType.LeftParen))
            {
                left = ParseCall(left);
            }
            else if (_stream.Match(TokenType.Dot))
            {
                Token member = _stream.Consume(TokenType.Identifier);
                left = new MemberAccessExpressionNode(left, member);
            }
            else if (_stream.Match(TokenType.LeftBracket))
            {
                ExpressionNode index = ParseExpression();
                _stream.Consume(TokenType.RightBracket);
                left = new IndexExpressionNode(left, index);
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private ExpressionNode ParseBasePrimary()
    {
        if (_stream.Match(TokenType.Identifier))
        {
            return new IdentifierExpressionNode(_stream.Previous);
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
            ExpressionNode expr = ParseExpression();
            _stream.Consume(TokenType.RightParen);
            return expr;
        }

        if (_stream.Match(TokenType.Minus) || _stream.Match(TokenType.KeywordNot))
        {
            Token op = _stream.Previous;
            ExpressionNode operand = ParsePrimary();
            return new UnaryExpressionNode(op, operand);
        }

        if (_stream.Match(TokenType.LeftBracket))
        {
            return ParseListLiteral();
        }

        _stream.ReportError(
            $"Unexpected token '{_stream.Current.Value}' in expression",
            _stream.Current);

        _stream.Advance();

        return new LiteralExpressionNode(new(TokenType.Unknown, "unknown", -1, -1));
    }

    private CallExpressionNode ParseCall(ExpressionNode callee)
    {
        List<ExpressionNode> args = [];

        if (!_stream.Match(TokenType.RightParen))
        {
            do
            {
                args.Add(ParseExpression());
            } while (_stream.Match(TokenType.Comma));

            _stream.Consume(TokenType.RightParen);
        }

        return new(callee, args);
    }

    private ListExpressionNode ParseListLiteral()
    {
        List<ExpressionNode> elements = [];

        if (!_stream.Match(TokenType.RightBracket))
        {
            do
            {
                elements.Add(ParseExpression());
            } while (_stream.Match(TokenType.Comma));
            _stream.Consume(TokenType.RightBracket);
        }

        return new(elements);
    }

    private static int GetPrecedence(TokenType type)
    {
        return type switch
        {
            TokenType.KeywordOr => 1,
            TokenType.KeywordAnd => 2,
            TokenType.DoubleEquals
                or TokenType.NotEquals
                or TokenType.LessThan
                or TokenType.GreaterThan
                or TokenType.LessEqual
                or TokenType.GreaterEqual => 3,
            TokenType.Plus or TokenType.Minus => 4,
            TokenType.Star or TokenType.Slash or TokenType.Percent => 5,
            TokenType.DoubleStar => 6,
            _ => -1
        };
    }
}