using Snek.Ast;
using Snek.Lexer;

namespace Snek.Parser;

public class StatementParser
{
    private readonly ParserStream _stream;
    private readonly ExpressionParser _expressions;
    private readonly LexerRules _rules;
    private int _expectedIndent;

    public StatementParser(ParserStream stream, ExpressionParser expressions, LexerRules rules)
    {
        _stream = stream;
        _expressions = expressions;
        _rules = rules;
        _expectedIndent = 0;
    }

    public ProgramNode ParseProgram()
    {
        List<StatementNode> statements = [];

        while (!_stream.Match(TokenType.Eof))
        {
            if (_stream.Match(TokenType.Newline))
            {
                continue;
            }

            // Handle top-level indentation adjustments (if any)
            if (_stream.Match(TokenType.Dedent))
            {
                _expectedIndent -= _rules.TabWidth;
                continue;
            }

            if (_stream.Match(TokenType.Indent))
            {
                _expectedIndent += _rules.TabWidth;
                continue;
            }

            StatementNode? stmt = ParseStatement();

            if (stmt == null)
            {
                continue;
            }

            statements.Add(stmt);
        }

        return new(statements);
    }

    private StatementNode? ParseStatement()
    {
        if (_stream.Match(TokenType.KeywordFn) || _stream.Match(TokenType.KeywordDef))
        {
            return ParseFunctionDef();
        }

        if (_stream.Match(TokenType.KeywordIf))
        {
            return ParseIfStatement();
        }

        if (_stream.Match(TokenType.KeywordWhile))
        {
            return ParseWhileStatement();
        }

        if (_stream.Match(TokenType.KeywordReturn))
        {
            return ParseReturnStatement();
        }

        if (_stream.Match(TokenType.KeywordPass))
        {
            ExpectNewline();
            return new PassStatementNode();
        }

        if (_stream.Match(TokenType.KeywordBreak))
        {
            ExpectNewline();
            return new BreakStatementNode();
        }

        if (_stream.Match(TokenType.KeywordContinue))
        {
            ExpectNewline();
            return new ContinueStatementNode();
        }

        // Check for variable declaration: identifier ':' type ('=' expression)?
        if (_stream.Current.Type == TokenType.Identifier && _stream.Peek().Type == TokenType.Colon)
        {
            return ParseVariableDeclaration();
        }

        ExpressionNode expr = _expressions.ParseExpression();
        ExpectNewline();
        return new ExpressionStatementNode(expr);
    }

    private FunctionDefNode ParseFunctionDef()
    {
        Token name = _stream.Consume(TokenType.Identifier);
        _stream.Consume(TokenType.LeftParen);
        List<ParameterNode> parameters = ParseParameters();

        TypeNode? returnType = null;

        if (_stream.Match(TokenType.Arrow))
        {
            returnType = ParseTypeAnnotation();
        }

        _stream.Consume(TokenType.Colon);
        ExpectNewline();

        int bodyIndent = _expectedIndent + _rules.TabWidth;
        List<StatementNode> body = ParseIndentedBlock();

        return new(name, parameters, returnType, body, bodyIndent);
    }

    private VariableDeclarationNode ParseVariableDeclaration()
    {
        Token name = _stream.Consume(TokenType.Identifier);
        _stream.Consume(TokenType.Colon);
        TypeNode type = ParseTypeAnnotation();
        
        ExpressionNode? initializer = null;
        
        if (_stream.Match(TokenType.Equals))
        {
            initializer = _expressions.ParseExpression();
        }
        
        ExpectNewline();
        return new VariableDeclarationNode(name, type, initializer, _expectedIndent);
    }

    private List<ParameterNode> ParseParameters()
    {
        List<ParameterNode> parameters = [];

        if (_stream.Match(TokenType.RightParen))
        {
            return parameters;
        }

        do
        {
            Token paramName = _stream.Consume(TokenType.Identifier);

            TypeNode? typeAnn = null;

            if (_stream.Match(TokenType.Colon))
            {
                typeAnn = ParseTypeAnnotation();
            }

            ExpressionNode? defaultValue = null;

            if (_stream.Match(TokenType.Equals))
            {
                defaultValue = _expressions.ParseExpression();
            }

            parameters.Add(new(paramName, typeAnn, defaultValue));
        } while (_stream.Match(TokenType.Comma));

        _stream.Consume(TokenType.RightParen);
        return parameters;
    }

    private TypeNode ParseTypeAnnotation()
    {
        Token nameToken;

        if (_stream.Match(TokenType.Identifier))
        {
            nameToken = _stream.Previous;
        }
        else
        {
            if (_stream.Match(TokenType.KeywordChar) ||
                 _stream.Match(TokenType.KeywordString) || _stream.Match(TokenType.KeywordBool))
            {
                nameToken = _stream.Previous;
            }
            else
            {
                nameToken = _stream.Consume(TokenType.Identifier);
            }
        }

        if (!_stream.Match(TokenType.LessThan))
        {
            return TypeNode.Simple(nameToken);
        }

        List<TypeNode> args = [];

        do
        {
            args.Add(ParseTypeAnnotation());
        }
        while (_stream.Match(TokenType.Comma));

        _stream.Consume(TokenType.GreaterThan);
        return TypeNode.Generic(nameToken, args);
    }

    private IfStatementNode ParseIfStatement()
    {
        ExpressionNode condition = _expressions.ParseExpression();
        _stream.Consume(TokenType.Colon);
        ExpectNewline();

        int thenIndent = _expectedIndent + _rules.TabWidth;
        List<StatementNode> thenBody = ParseIndentedBlock();

        List<StatementNode>? elseBody = null;

        if (!_stream.Match(TokenType.KeywordElse))
        {
            return new(condition, thenBody, elseBody, thenIndent);
        }

        _stream.Consume(TokenType.Colon);
        ExpectNewline();

        _ = _expectedIndent + _rules.TabWidth;

        elseBody = ParseIndentedBlock();

        return new(condition, thenBody, elseBody, thenIndent);
    }

    private WhileStatementNode ParseWhileStatement()
    {
        ExpressionNode condition = _expressions.ParseExpression();
        _stream.Consume(TokenType.Colon);
        ExpectNewline();

        int bodyIndent = _expectedIndent + _rules.TabWidth;
        List<StatementNode> body = ParseIndentedBlock();

        return new WhileStatementNode(condition, body, bodyIndent);
    }

    private ReturnStatementNode ParseReturnStatement()
    {
        ExpressionNode? value = null;

        if (_stream.Current.Type is not (TokenType.Newline or TokenType.Eof or TokenType.Dedent))
        {
            value = _expressions.ParseExpression();
        }

        ExpectNewline();
        return new ReturnStatementNode(value);
    }

    private List<StatementNode> ParseIndentedBlock()
    {
        if (!_stream.Match(TokenType.Indent))
        {
            _stream.ReportError(
                $"Expected indented block but got '{_stream.Current.Value}'",
                _stream.Current);

            SyncToBlockEnd();
            return [];
        }

        List<StatementNode> statements = [];

        while (!_stream.Match(TokenType.Dedent) && !_stream.Match(TokenType.Eof))
        {
            if (_stream.Match(TokenType.Newline))
            {
                continue;
            }

            StatementNode? stmt = ParseStatement();

            if (stmt == null)
            {
                continue;
            }

            statements.Add(stmt);
        }
        return statements;
    }

    private void SyncToBlockEnd()
    {
        while (_stream.Current.Type != TokenType.Dedent &&
               _stream.Current.Type != TokenType.Eof)
        {
            _stream.Advance();
        }

        _stream.Match(TokenType.Dedent);
    }

    private void ExpectNewline()
    {
        if (_stream.Match(TokenType.Newline) || _stream.Match(TokenType.Eof))
        {
            return;
        }

        // Inside an indented block, Dedent follows the last statement
        if (_stream.Current.Type == TokenType.Dedent)
        {
            return;
        }

        _stream.ReportError(
            $"Expected newline after ':' but got '{_stream.Current.Value}'",
            _stream.Current);

        // Skip to next line to prevent cascading errors
        SyncToNewline();
    }

    private void SyncToNewline()
    {
        while (_stream.Current.Type != TokenType.Newline &&
               _stream.Current.Type != TokenType.Dedent &&
               _stream.Current.Type != TokenType.Eof)
        {
            _stream.Advance();
        }

        _stream.Match(TokenType.Newline);
    }
}