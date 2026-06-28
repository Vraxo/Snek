using Snek.Core.Compiler;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Snek;

[Description("Snek Compiler - compiles .snek files to executables")]
public class CompileCommand : Command<CompilerSettings>
{
    protected override int Execute(
        [NotNull] CommandContext context,
        [NotNull] CompilerSettings settings,
        CancellationToken cancellationToken)
    {
        CompilerOptions options = new()
        {
            OutputPath = settings.OutputPath,
            Syntax = settings.Syntax,
            Verbose = settings.Verbose,
            AsmOnly = settings.AsmOnly
        };

        CompilerService compiler = new(options);
        (bool success, _, _) = compiler.Compile(settings.InputPath);

        return success ? 0 : 1;
    }
}