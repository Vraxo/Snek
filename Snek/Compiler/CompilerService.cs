using Snek.Analysis;
using Snek.Diagnoistics;
using Snek.Generation;
using Snek.Lexer;
using Snek.Pipeline;

namespace Snek.Compiler;

public class CompilerService
{
    private readonly CompilerOptions _options;

    public CompilerService(CompilerOptions options)
    {
        _options = options;
    }

    public (bool Success, string? AssemblyPath, string? ExecutablePath) Compile(string sourcePath)
    {
        if (!File.Exists(sourcePath))
        {
            Console.Error.WriteLine($"Error: Input file not found: {sourcePath}");
            return (false, null, null);
        }

        string source = File.ReadAllText(sourcePath);
        PipelineOptions pipelineOptions = new() { EnableLogging = _options.Verbose };

        LexerRules lexerRules = GetLexerRules(_options.Syntax);
        Lexer.Lexer lexer = new(lexerRules);
        Parser.Parser parser = new(lexerRules);
        SemanticAnalyzer analyzer = new();
        CodeGenerator generator = new();

        CompilerPipeline pipeline = new(lexer, parser, analyzer, generator, pipelineOptions);
        CompilationResult result = pipeline.Compile(source, sourcePath);

        if (!result.Success)
        {
            PrintDiagnostics(source, sourcePath, result.Diagnostics);
            return (false, null, null);
        }

        string asmOutputPath = _options.OutputPath ?? "output.asm";
        string exeOutputPath = _options.OutputPath?.Replace(".asm", ".exe") ?? "output.exe";

        File.WriteAllText(asmOutputPath, result.Output ?? string.Empty);
        Console.WriteLine($"Assembly generated: {asmOutputPath}");

        if (_options.AsmOnly)
        {
            return (true, asmOutputPath, null);
        }

        string asmDirectory = Path.GetDirectoryName(Path.GetFullPath(asmOutputPath)) ?? ".";
        if (Assembler.Assemble(asmOutputPath, asmDirectory))
        {
            Console.WriteLine($"Executable created: {exeOutputPath}");
            return (true, asmOutputPath, exeOutputPath);
        }
        else
        {
            Console.Error.WriteLine("Assembly failed. Check FASM output above.");
            return (false, asmOutputPath, null);
        }
    }

    private void PrintDiagnostics(string source, string sourcePath, IReadOnlyList<Diagnostic> diagnostics)
    {
        string[] sourceLines = source.ReplaceLineEndings("\n").Split('\n');
        Dictionary<string, string[]> sourceFiles = new()
        {
            [sourcePath] = sourceLines
        };
        IReadOnlyList<Diagnostic> deduped = DeduplicateDiagnostics(diagnostics);
        DiagnosticPrinter printer = new(deduped, sourceFiles);
        printer.Print();
    }

    private static IReadOnlyList<Diagnostic> DeduplicateDiagnostics(IReadOnlyList<Diagnostic> diagnostics)
    {
        List<Diagnostic> deduped = [];
        HashSet<(string, int)> seenLines = [];

        foreach (Diagnostic diag in diagnostics
            .OrderBy(d => d.SourceName)
            .ThenBy(d => d.Line)
            .ThenBy(d => d.Column))
        {
            if (seenLines.Add((diag.SourceName, diag.Line)))
            {
                deduped.Add(diag);
            }
        }

        return deduped;
    }

    private static LexerRules GetLexerRules(string syntax)
    {
        return syntax?.ToLowerInvariant() switch
        {
            "python" => LexerRules.CreatePythonStyle(),
            _ => new()
        };
    }
}