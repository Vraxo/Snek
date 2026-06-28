using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Snek;

[Description("Removes Snek from the system PATH environment variable")]
public class UninstallCommand : Command<UninstallCommand.Settings>
{
    public class Settings : CommandSettings { }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            AnsiConsole.MarkupLine("[red]Error: Could not retrieve current executable path.[/]");
            return 1;
        }

        string? exeDir = Path.GetDirectoryName(exePath);
        if (string.IsNullOrEmpty(exeDir))
        {
            AnsiConsole.MarkupLine("[red]Error: Could not retrieve executable directory.[/]");
            return 1;
        }

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                UninstallWindows(exeDir);
            }
            else
            {
                UninstallUnix();
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error during uninstallation:[/] {ex.Message}");
            return 1;
        }
    }

    private static void UninstallWindows(string exeDir)
    {
        string? currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);
        if (string.IsNullOrEmpty(currentPath))
        {
            AnsiConsole.MarkupLine("[yellow]No User PATH environment variables found.[/]");
            return;
        }

        string[] paths = currentPath.Split(';', StringSplitOptions.RemoveEmptyEntries);
        List<string> filteredPaths = paths
            .Where(p => !string.Equals(p.Trim(), exeDir.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (paths.Length == filteredPaths.Count)
        {
            AnsiConsole.MarkupLine("[yellow]Snek directory was not found in your User PATH.[/]");
            return;
        }

        string newPath = string.Join(";", filteredPaths);
        Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.User);

        AnsiConsole.MarkupLine("[green]Successfully removed Snek directory from your User PATH![/]");
        AnsiConsole.MarkupLine("[blue]Please restart your active terminal sessions for the changes to take effect.[/]");
    }

    private static void UninstallUnix()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string linkPath = Path.Combine(userProfile, ".local", "bin", "snek");

        if (File.Exists(linkPath))
        {
            File.Delete(linkPath);
            AnsiConsole.MarkupLine($"[green]Successfully deleted '{linkPath}'![/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Global Snek command '~/.local/bin/snek' was not found.[/]");
        }
    }
}