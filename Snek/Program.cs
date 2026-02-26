using Snek.Analysis;
using Snek.Compiler;
using Snek.Generation;
using Snek.Lexer;
using Snek.Parser;
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

        var inputPath = args[0];
        var options = ParseOptions(args);

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Error: Input file not found: {inputPath}");
            return;
        }

        var source = File.ReadAllText(inputPath);
        var pipelineOptions = new PipelineOptions { EnableLogging = options.Verbose };

        var lexerRules = GetLexerRules(options.Syntax);
        var lexer = new SnekLexer(lexerRules);
        var parser = new SnekParser(lexerRules);
        var analyzer = new SnekSemanticAnalyzer();
        var generator = new SnekCodeGenerator();

        var pipeline = new CompilerPipeline(lexer, parser, analyzer, generator, pipelineOptions);
        var result = pipeline.Compile(source, inputPath);

        if (!result.Success)
        {
            foreach (var diag in result.Diagnostics)
            {
                Console.Error.WriteLine(diag.ToString());
            }
            return;
        }

        var asmOutputPath = options.OutputPath ?? "output.asm";
        var exeOutputPath = options.OutputPath?.Replace(".asm", ".exe") ?? "output.exe";

        File.WriteAllText(asmOutputPath, result.Output ?? string.Empty);
        Console.WriteLine($"Assembly generated: {asmOutputPath}");

        if (!options.AsmOnly)
        {
            var asmDirectory = Path.GetDirectoryName(Path.GetFullPath(asmOutputPath)) ?? ".";
            var assembler = new Assembler();

            if (assembler.Assemble(asmOutputPath, asmDirectory))
            {
                Console.WriteLine($"Executable created: {exeOutputPath}");
            }
            else
            {
                Console.Error.WriteLine("Assembly failed. Check FASM output above.");
                return;
            }
        }
    }

    private static CompilerOptions ParseOptions(string[] args)
    {
        var options = new CompilerOptions();
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
            "cstyle" => LexerRules.CreateCStyle(),
            "python" => LexerRules.CreatePythonStyle(),
            _ => new LexerRules()
        };
    }
}

public record CompilerOptions
{
    public string? OutputPath { get; set; }
    public string? Syntax { get; set; }
    public bool Verbose { get; set; }
    public bool AsmOnly { get; set; }
}