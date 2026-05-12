using Snek.Analysis;
using Snek.Compiler;
using Snek.Diagnoistics;
using Snek.Generation;
using Snek.Lexer;
using Snek.Pipeline;

namespace Snek;

public class Program
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Snek Compiler v0.1");
            Console.WriteLine("Usage: snek <input.snek> [options]");
            Console.WriteLine("Options:");
            Console.WriteLine("  --output <file>  Specify output file (default: output.exe)");
            Console.WriteLine("  --syntax <name>  Use alternate syntax: python, cstyle (default: python)");
            Console.WriteLine("  --verbose        Enable detailed logging");
            Console.WriteLine("  --asm-only       Stop after generating assembly (do not assemble)");
            return;
        }

        string inputPath = args[0];
        CompilerOptions options = ParseOptions(args);

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Error: Input file not found: {inputPath}");
            return;
        }

        string source = File.ReadAllText(inputPath);
        PipelineOptions pipelineOptions = new() { EnableLogging = options.Verbose };

        LexerRules lexerRules = GetLexerRules(options.Syntax);
        Lexer.Lexer lexer = new(lexerRules);
        Parser.Parser parser = new(lexerRules);
        SemanticAnalyzer analyzer = new();
        CodeGenerator generator = new();

        CompilerPipeline pipeline = new(lexer, parser, analyzer, generator, pipelineOptions);
        CompilationResult result = pipeline.Compile(source, inputPath);

        if (!result.Success)
        {
            string[] sourceLines = source.ReplaceLineEndings("\n").Split('\n');
            Dictionary<string, string[]> sourceFiles = new()
            {
                [inputPath] = sourceLines
            };

            DiagnosticPrinter printer = new(result.Diagnostics, sourceFiles);
            printer.Print();

            return;
        }

        string asmOutputPath = options.OutputPath ?? "output.asm";

        string exeOutputPath = options.OutputPath?.Replace(".asm", ".exe")
            ?? "output.exe";

        File.WriteAllText(asmOutputPath, result.Output ?? string.Empty);
        Console.WriteLine($"Assembly generated: {asmOutputPath}");

        if (options.AsmOnly)
        {
            return;
        }

        string asmDirectory = Path.GetDirectoryName(Path.GetFullPath(asmOutputPath)) ?? ".";
        new Assembler();

        if (Assembler.Assemble(asmOutputPath, asmDirectory))
        {
            Console.WriteLine($"Executable created: {exeOutputPath}");
        }
        else
        {
            Console.Error.WriteLine("Assembly failed. Check FASM output above.");
            return;
        }
    }

    private static CompilerOptions ParseOptions(string[] args)
    {
        CompilerOptions options = new();

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--output":
                    if (i + 1 < args.Length)
                    {
                        options.OutputPath = args[++i];
                    }
                    break;

                case "--syntax":
                    if (i + 1 < args.Length)
                    {
                        options.Syntax = args[++i];
                    }
                    break;

                case "--verbose":
                    options.Verbose = true;
                    break;

                case "--asm-only":
                    options.AsmOnly = true;
                    break;
            }
        }
        return options;
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