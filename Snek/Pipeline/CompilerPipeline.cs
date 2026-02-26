using Snek.Analysis;
using Snek.Diagnoistics;
using Snek.Generation;
using Snek.Lexer;
using Snek.Parser;

namespace Snek.Pipeline;

/// <summary>
/// Modular compilation pipeline where each stage is swappable via dependency injection.
/// Enables syntax-agnostic compilation by abstracting lexer/parser behind interfaces.
/// </summary>
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
        var context = new CompilationContext(sourceName, _options);

        try
        {
            // Stage 1: Lexing
            if (_options.EnableLogging)
            {
                Console.WriteLine($"[{sourceName}] Lexing...");
            }

            var tokens = _lexer.Tokenize(source, context);
            if (context.Diagnostics.Any(d => d.IsError))
            {
                return new CompilationResult(context.Diagnostics);
            }

            // Stage 2: Parsing
            if (_options.EnableLogging)
            {
                Console.WriteLine($"[{sourceName}] Parsing...");
            }

            var ast = _parser.Parse(tokens, context);
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

            var output = _generator.Generate(ast, context);
            return context.Diagnostics.Any(d => d.IsError)
                ? new CompilationResult(context.Diagnostics)
                : new CompilationResult(output, context.Diagnostics);
        }
        catch (Exception ex)
        {
            context.Diagnostics.Add(new Diagnostic(sourceName, $"Internal compiler error: {ex.Message}", -1, -1, DiagnosticSeverity.Error));
            return new CompilationResult(context.Diagnostics);
        }
    }
}

public record PipelineOptions
{
    public bool EnableLogging { get; set; } = false;
    public bool EnableOptimizations { get; set; } = false;
    public TargetPlatform Target { get; set; } = TargetPlatform.X86;
}

public enum TargetPlatform { X86, X64, WebAssembly }

public record CompilationResult(string? Output, IReadOnlyList<Diagnostic> Diagnostics)
{
    public CompilationResult(IReadOnlyList<Diagnostic> diagnostics) : this(null, diagnostics) { }
    public bool Success => !Diagnostics.Any(d => d.IsError);
}