using Snek.Core.Analysis;
using Snek.Core.Diagnoistics;
using Snek.Core.Generation;
using Snek.Core.Lexing;
using Snek.Core.Parsing;
using Snek.Core.Pipeline;

namespace Snek.Core.Compiler;

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
            ReportFileNotFound(sourcePath);
            return (false, null, null);
        }

        string source = File.ReadAllText(sourcePath);
        CompilationResult result = RunCompilerPipeline(source, sourcePath);

        if (!result.Success)
        {
            PrintDiagnostics(source, sourcePath, result.Diagnostics);
            return (false, null, null);
        }

        string asmOutputPath = DetermineAssemblyOutputPath();
        WriteAssemblyFile(asmOutputPath, result.Output);

        if (_options.AsmOnly)
        {
            return (true, asmOutputPath, null);
        }

        bool assemblySucceeded = RunAssembler(asmOutputPath);
        if (assemblySucceeded)
        {
            string exeOutputPath = DetermineExecutableOutputPath();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("[Snek] ");
            Console.ResetColor();
            Console.WriteLine($"Created executable: {exeOutputPath}");
            return (true, asmOutputPath, exeOutputPath);
        }
        else
        {
            return (false, asmOutputPath, null);
        }
    }

    private CompilationResult RunCompilerPipeline(string source, string sourcePath)
    {
        PipelineOptions pipelineOptions = new()
        {
            EnableLogging = _options.Verbose
        };

        LexerRules lexerRules = DetermineLexerRules();
        Lexer lexer = new(lexerRules);
        Parser parser = new(lexerRules);
        SemanticAnalyzer analyzer = new();
        CodeGenerator generator = new();

        CompilerPipeline pipeline = new(lexer, parser, analyzer, generator, pipelineOptions);

        return pipeline.Compile(source, sourcePath);
    }

    private LexerRules DetermineLexerRules()
    {
        string syntax = _options.Syntax ?? string.Empty;

        return syntax.ToLowerInvariant() switch
        {
            "python" => LexerRules.CreatePythonStyle(),
            _ => new()
        };
    }

    private string DetermineAssemblyOutputPath()
    {
        return _options.OutputPath ?? "output.asm";
    }

    private string DetermineExecutableOutputPath()
    {
        return _options.OutputPath?.Replace(".asm", ".exe") ?? "output.exe";
    }

    private void WriteAssemblyFile(string asmOutputPath, string? assemblyContent)
    {
        File.WriteAllText(asmOutputPath, assemblyContent ?? string.Empty);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("[Snek] ");
        Console.ResetColor();
        Console.WriteLine($"Generated assembly: {asmOutputPath}");
    }

    private bool RunAssembler(string asmOutputPath)
    {
        string asmDirectory = Path.GetDirectoryName(Path.GetFullPath(asmOutputPath)) ?? ".";
        bool success = Assembler.Assemble(asmOutputPath, asmDirectory);

        if (!success)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.Write("[Snek] ");
            Console.ResetColor();
            Console.Error.WriteLine("Assembly failed. Check FASM output above.");
        }

        return success;
    }

    private static void ReportFileNotFound(string sourcePath)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.Write("[Snek] ");
        Console.ResetColor();
        Console.Error.WriteLine($"Error: Input file not found: {sourcePath}");
    }

    private static void PrintDiagnostics(string source, string sourcePath, IReadOnlyList<Diagnostic> diagnostics)
    {
        string[] sourceLines = source.ReplaceLineEndings("\n").Split('\n');

        Dictionary<string, string[]> sourceFiles = new()
        {
            [sourcePath] = sourceLines
        };

        IReadOnlyList<Diagnostic> uniqueDiagnostics = DeduplicateDiagnostics(diagnostics);
        DiagnosticPrinter printer = new(uniqueDiagnostics, sourceFiles);

        printer.Print();
    }

    private static IReadOnlyList<Diagnostic> DeduplicateDiagnostics(IReadOnlyList<Diagnostic> diagnostics)
    {
        List<Diagnostic> deduplicated = [];
        HashSet<(string Source, int Line)> seen = [];

        foreach (Diagnostic diagnostic in diagnostics
            .OrderBy(d => d.SourceName)
            .ThenBy(d => d.Line)
            .ThenBy(d => d.Column))
        {
            if (seen.Add((diagnostic.SourceName, diagnostic.Line)))
            {
                deduplicated.Add(diagnostic);
            }
        }

        return deduplicated;
    }
}