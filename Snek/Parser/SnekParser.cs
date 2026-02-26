using Snek.Ast;
using Snek.Lexer;
using Snek.Pipeline;

namespace Snek.Parser;

/// <summary>
/// Orchestrator for the parsing process.
/// Delegates actual parsing logic to specialized StatementParser and ExpressionParser components.
/// </summary>
public class SnekParser : IParser
{
    private readonly LexerRules _rules;

    public SnekParser(LexerRules? rules = null)
    {
        _rules = rules ?? new LexerRules();
    }

    public AstNode Parse(IEnumerable<Token> tokens, CompilationContext context)
    {
        var stream = new ParserStream(tokens, context);
        var expressionParser = new ExpressionParser(stream);
        var statementParser = new StatementParser(stream, expressionParser, _rules);

        return statementParser.ParseProgram();
    }
}