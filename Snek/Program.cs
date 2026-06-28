using Spectre.Console.Cli;

namespace Snek;

public class Program
{
    public static int Main(string[] args)
    {
        CommandApp<CompileCommand> app = new();
        app.Configure(config =>
        {
            config.SetApplicationName("snek");
            config.SetApplicationVersion("1.0.0");
        });

        return app.Run(args);
    }
}