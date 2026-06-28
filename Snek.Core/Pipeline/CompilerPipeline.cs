using Snek.Core.Analysis;
using Snek.Core.Diagnoistics;
using Snek.Core.Generation;
using Snek.Core.Lexing;
using Snek.Core.Parsing;

namespace Snek.Core.Pipeline;

public class CompilerPipeline
{
    private readonly ILexer _lexer;
    private readonly IParser _parser;
    private readonly ISemanticAnalyzer _analyzer;
    private readonly ICodeGenerator _generator;
    private readonly PipelineOptions _options;

    public CompilerPipeline(
        ILexer lexer,
        IParser parser,
        ISemanticAnalyzer analyzer,
        ICodeGenerator generator,
        PipelineOptions? options = null)
    {
        _lexer = lexer ?? throw new ArgumentNullException(nameof(lexer));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _options = options ?? new PipelineOptions();
    }

    public CompilationResult Compile(string source, string sourceName = "<input>")
    {
        CompilationContext context = new(sourceName, _options);

        try
        {
            // Stage 1: Lexing
            if (_options.EnableLogging)
            {
                Console.WriteLine($"[{sourceName}] Lexing...");
            }

            IEnumerable<Token> tokens = _lexer.Tokenize(source, context);

            // Continue to parsing even with lexer errors — the parser
            // can often report better, more context-aware diagnostics.
            // Stage 2: Parsing
            if (_options.EnableLogging)
            {
                Console.WriteLine($"[{sourceName}] Parsing...");
            }

            AstNode ast = _parser.Parse(tokens, context);
            if (context.Diagnostics.Any(d => d.IsError))
            {
                return new CompilationResult(context.Diagnostics);
            }

            // Stage 3: Semantic Analysis
            if (_options.EnableLogging)
            {
                Console.WriteLine($"[{sourceName}] Analyzing...");
            }

            _analyzer.Analyze(ast, context);
            if (context.Diagnostics.Any(d => d.IsError))
            {
                return new CompilationResult(context.Diagnostics);
            }

            // Stage 4: Code Generation
            if (_options.EnableLogging)
            {
                Console.WriteLine($"[{sourceName}] Generating...");
            }

            string? output = _generator.Generate(ast, context);

            return context.Diagnostics.Any(d => d.IsError)
                ? new(context.Diagnostics)
                : new(output, context.Diagnostics);
        }
        catch (Exception ex)
        {
            context.Diagnostics.Add(new(
                sourceName,
                $"Internal compiler error: {ex.Message}",
                -1,
                -1,
                DiagnosticSeverity.Error));

            return new(context.Diagnostics);
        }
    }
}