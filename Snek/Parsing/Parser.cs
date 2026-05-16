using Snek.Ast;
using Snek.Lexing;
using Snek.Pipeline;

namespace Snek.Parsing;

public class Parser : IParser
{
    private readonly LexerRules _rules;

    public Parser(LexerRules? rules = null)
    {
        _rules = rules ?? new();
    }

    public AstNode Parse(IEnumerable<Token> tokens, CompilationContext context)
    {
        ParserStream stream = new(tokens, context);
        ExpressionParser expressionParser = new(stream);
        StatementParser statementParser = new(stream, expressionParser, _rules);

        return statementParser.ParseProgram();
    }
}