using Snek.Compiler;
using System.CommandLine;

namespace Snek;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        RootCommand rootCommand = new("Snek Compiler - compiles .snek files to executables");

        Argument<string> inputArgument = new("input")
        {
            Description = "Path to the input .snek file"
        };
        rootCommand.AddArgument(inputArgument);

        Option<string> outputOption = new(
            "--output",
            "Specify output file (default: output.asm or output.exe)");

        outputOption.AddAlias("-o");
        rootCommand.AddOption(outputOption);

        Option<string> syntaxOption = new(
            "--syntax",
            () => "python",
            "Use alternate syntax: python, cstyle (default: python)");

        rootCommand.AddOption(syntaxOption);

        Option<bool> verboseOption = new(
            "--verbose",
            "Enable detailed logging");

        verboseOption.AddAlias("-v");
        rootCommand.AddOption(verboseOption);

        Option<bool> asmOnlyOption = new(
            "--asm-only",
            "Stop after generating assembly (do not assemble)");
        rootCommand.AddOption(asmOnlyOption);

        rootCommand.SetHandler(async (inputPath, outputPath, syntax, verbose, asmOnly) =>
        {
            await Task.Run(() =>
            {
                CompilerOptions options = new()
                {
                    OutputPath = outputPath,
                    Syntax = syntax,
                    Verbose = verbose,
                    AsmOnly = asmOnly
                };

                CompilerService compiler = new(options);
                (bool success, string? assemblyPath, string? executablePath) = compiler.Compile(inputPath);
                Environment.ExitCode = success ? 0 : 1;
            });
        }, inputArgument, outputOption, syntaxOption, verboseOption, asmOnlyOption);

        return await rootCommand.InvokeAsync(args);
    }
}