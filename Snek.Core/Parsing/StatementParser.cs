using Snek.Core.Ast;
using Snek.Core.Lexing;

namespace Snek.Core.Parsing;

public class StatementParser
{
    private readonly ParserStream _stream;
    private readonly ExpressionParser _expressions;

    public StatementParser(ParserStream stream, ExpressionParser expressions, LexerRules rules)
    {
        _stream = stream;
        _expressions = expressions;
    }

    public ProgramNode ParseProgram()
    {
        List<StatementNode> statements = [];

        while (!_stream.Match(TokenType.Eof))
        {
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
        bool isPub = _stream.Match(TokenType.KeywordPub);

        if (_stream.Match(TokenType.KeywordImport))
        {
            return ParseImport();
        }

        if (_stream.Match(TokenType.KeywordMod))
        {
            return ParseMod();
        }

        if (_stream.Match(TokenType.KeywordUse))
        {
            return ParseUse();
        }

        if (_stream.Match(TokenType.KeywordImpl))
        {
            return ParseImplBlock();
        }

        if (_stream.Match(TokenType.KeywordClass))
        {
            return ParseClassDef(isPub);
        }

        if (_stream.Match(TokenType.KeywordExtern))
        {
            return ParseExternFunctionDef(isPub);
        }

        if (_stream.Match(TokenType.KeywordFn) || _stream.Match(TokenType.KeywordDef))
        {
            return ParseFunctionDef(isPub);
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
            StatementNode ret = ParseReturnStatement();
            _stream.Consume(TokenType.Semicolon);
            return ret;
        }

        if (_stream.Match(TokenType.KeywordPass))
        {
            _stream.Consume(TokenType.Semicolon);
            return new PassStatementNode();
        }

        if (_stream.Match(TokenType.KeywordBreak))
        {
            _stream.Consume(TokenType.Semicolon);
            return new BreakStatementNode();
        }

        if (_stream.Match(TokenType.KeywordContinue))
        {
            _stream.Consume(TokenType.Semicolon);
            return new ContinueStatementNode();
        }

        // Check for variable declaration: identifier ':' type ('=' expression)?
        if (_stream.Current.Type == TokenType.Identifier && _stream.Peek().Type == TokenType.Colon)
        {
            StatementNode decl = ParseVariableDeclaration();
            _stream.Consume(TokenType.Semicolon);
            return decl;
        }

        // Parse expression first to support generalized assignment statements (x = val, p.x = val, arr[idx] = val)
        ExpressionNode leftExpr = _expressions.ParseExpression();

        if (_stream.Current.Type is TokenType.Equals or TokenType.PlusAssign or TokenType.MinusAssign)
        {
            TokenType op = _stream.Current.Type;
            _stream.Advance(); // consume assignment operator
            ExpressionNode value = _expressions.ParseExpression();
            _stream.Consume(TokenType.Semicolon);

            if (op == TokenType.Equals)
            {
                return new AssignmentStatementNode(leftExpr, value);
            }
            else
            {
                TokenType binaryOp = op == TokenType.PlusAssign ? TokenType.Plus : TokenType.Minus;
                Token opToken = new(binaryOp, binaryOp == TokenType.Plus ? "+" : "-", _stream.Current.Line, _stream.Current.Column);
                BinaryExpressionNode desugared = new(leftExpr, opToken, value);
                return new AssignmentStatementNode(leftExpr, desugared);
            }
        }

        _stream.Consume(TokenType.Semicolon);
        return new ExpressionStatementNode(leftExpr);
    }

    private ImportStatementNode ParseImport()
    {
        Token moduleToken = _stream.Consume(TokenType.Identifier);
        _stream.Consume(TokenType.Semicolon);
        return new ImportStatementNode(moduleToken.Value);
    }

    private ModuleDeclarationNode ParseMod()
    {
        Token moduleToken = _stream.Consume(TokenType.Identifier);
        _stream.Consume(TokenType.Semicolon);
        return new ModuleDeclarationNode(moduleToken);
    }

    private UseStatementNode ParseUse()
    {
        List<Token> path = [];

        do
        {
            if (_stream.Match(TokenType.Star))
            {
                _stream.Consume(TokenType.Semicolon);
                return new UseStatementNode(path, true);
            }

            Token part = _stream.Consume(TokenType.Identifier);
            path.Add(part);

        } while (_stream.Match(TokenType.DoubleColon));

        _stream.Consume(TokenType.Semicolon);
        return new UseStatementNode(path, false);
    }

    private FunctionDefNode ParseFunctionDef(bool isPub)
    {
        Token name = _stream.Consume(TokenType.Identifier);
        _stream.Consume(TokenType.LeftParen);
        List<ParameterNode> parameters = ParseParameters();

        TypeNode? returnType = null;

        if (_stream.Match(TokenType.Arrow))
        {
            returnType = ParseTypeAnnotation();
        }

        List<StatementNode> body = ParseBlock();

        return new(name, parameters, returnType, body, isPub);
    }

    private ImplBlockNode ParseImplBlock()
    {
        Token targetClass = _stream.Consume(TokenType.Identifier);
        _stream.Consume(TokenType.LeftBrace);
        List<FunctionDefNode> methods = [];

        while (!_stream.Match(TokenType.RightBrace) && !_stream.Match(TokenType.Eof))
        {
            bool isPub = _stream.Match(TokenType.KeywordPub);
            if (_stream.Match(TokenType.KeywordFn) || _stream.Match(TokenType.KeywordDef))
            {
                methods.Add(ParseFunctionDef(isPub));
            }
            else
            {
                _stream.Advance();
            }
        }

        return new ImplBlockNode(targetClass, methods);
    }

    private ClassDefNode ParseClassDef(bool isPub)
    {
        Token name = _stream.Consume(TokenType.Identifier);
        _stream.Consume(TokenType.LeftBrace);
        List<FieldNode> fields = [];

        while (!_stream.Match(TokenType.RightBrace) && !_stream.Match(TokenType.Eof))
        {
            Token fieldName = _stream.Consume(TokenType.Identifier);
            _stream.Consume(TokenType.Colon);
            TypeNode fieldType = ParseTypeAnnotation();
            _stream.Consume(TokenType.Semicolon);
            fields.Add(new FieldNode(fieldName, fieldType));
        }

        return new ClassDefNode(name, fields, isPub);
    }

    private ExternFunctionDefNode ParseExternFunctionDef(bool isPub)
    {
        _stream.Consume(TokenType.KeywordFn);
        Token name = _stream.Consume(TokenType.Identifier);
        _stream.Consume(TokenType.LeftParen);
        List<ParameterNode> parameters = ParseParameters();

        TypeNode? returnType = null;

        if (_stream.Match(TokenType.Arrow))
        {
            returnType = ParseTypeAnnotation();
        }

        _stream.Consume(TokenType.Semicolon);
        return new ExternFunctionDefNode(name, parameters, returnType, isPub);
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

        return new VariableDeclarationNode(name, type, initializer);
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
        List<StatementNode> thenBody = ParseBlock();

        List<StatementNode>? elseBody = null;

        if (_stream.Match(TokenType.KeywordElse))
        {
            if (_stream.Current.Type == TokenType.KeywordIf)
            {
                _stream.Advance(); // consume 'if'
                elseBody = [ParseIfStatement()];
            }
            else
            {
                elseBody = ParseBlock();
            }
        }

        return new(condition, thenBody, elseBody);
    }

    private WhileStatementNode ParseWhileStatement()
    {
        ExpressionNode condition = _expressions.ParseExpression();
        List<StatementNode> body = ParseBlock();

        return new WhileStatementNode(condition, body);
    }

    private ReturnStatementNode ParseReturnStatement()
    {
        ExpressionNode? value = null;

        if (_stream.Current.Type != TokenType.Semicolon)
        {
            value = _expressions.ParseExpression();
        }

        return new ReturnStatementNode(value);
    }

    private List<StatementNode> ParseBlock()
    {
        _stream.Consume(TokenType.LeftBrace);
        List<StatementNode> statements = [];

        while (!_stream.Match(TokenType.RightBrace) && !_stream.Match(TokenType.Eof))
        {
            StatementNode? stmt = ParseStatement();

            if (stmt == null)
            {
                continue;
            }

            statements.Add(stmt);
        }

        return statements;
    }
}