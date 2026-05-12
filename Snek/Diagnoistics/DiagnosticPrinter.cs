namespace Snek.Diagnoistics;

public class DiagnosticPrinter
{
    private readonly IReadOnlyList<Diagnostic> _diagnostics;
    private readonly IReadOnlyDictionary<string, string[]> _sourceFiles;

    public DiagnosticPrinter(IReadOnlyList<Diagnostic> diagnostics, IReadOnlyDictionary<string, string[]> sourceFiles)
    {
        _diagnostics = diagnostics;
        _sourceFiles = sourceFiles;
    }

    public void Print()
    {
        foreach (Diagnostic diagnostic in _diagnostics
            .OrderBy(d => d.SourceName)
            .ThenBy(d => d.Line)
            .ThenBy(d => d.Column))
        {
            if (!_sourceFiles.TryGetValue(diagnostic.SourceName, out string[]? lines) || diagnostic.Line < 1)
            {
                Console.Error.WriteLine(
                    $"{diagnostic.SourceName}({diagnostic.Line},{diagnostic.Column}): " +
                    $"{(diagnostic.IsError ? "error" : "warning")}: {diagnostic.Message}");
                continue;
            }

            Console.Error.WriteLine();

            ConsoleColor color = diagnostic.Severity switch
            {
                DiagnosticSeverity.Warning => ConsoleColor.Yellow,
                _ => ConsoleColor.Red
            };

            string prefix = diagnostic.Severity switch
            {
                DiagnosticSeverity.Warning => "Warning: ",
                _ => "Error: "
            };

            Console.ForegroundColor = color;
            Console.Error.Write(prefix);
            Console.ResetColor();
            Console.Error.WriteLine(diagnostic.Message);

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Error.WriteLine($"  --> {diagnostic.SourceName}:{diagnostic.Line}:{diagnostic.Column}");
            Console.Error.WriteLine("   |");

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Error.Write($"{diagnostic.Line,2} | ");
            Console.ResetColor();

            string line = lines[diagnostic.Line - 1];
            Console.Error.WriteLine(line);

            Console.ForegroundColor = ConsoleColor.DarkGray;
            int pad = int.Max(0, diagnostic.Column - 1);
            Console.Error.Write("   | ");
            Console.ForegroundColor = color;
            Console.Error.WriteLine(
                new string(' ', pad) + new string('^', diagnostic.Length));
            Console.ResetColor();
        }
    }
}