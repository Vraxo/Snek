using Spectre.Console.Cli;

namespace Snek;

public class Program
{
    public static int Main(string[] args)
    {
        CommandApp app = new();
        app.SetDefaultCommand<CompileCommand>();

        app.Configure(config =>
        {
            config.SetApplicationName("snek");
            config.SetApplicationVersion("1.0.0");

            config.AddCommand<InstallCommand>("install");
            config.AddCommand<UninstallCommand>("uninstall");
        });

        return app.Run(args);
    }
}