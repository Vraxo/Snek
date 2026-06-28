using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Snek;

[Description("Installs Snek to the system PATH environment variable for global access")]
public class InstallCommand : Command<InstallCommand.Settings>
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
                InstallWindows(exeDir);
            }
            else
            {
                InstallUnix(exePath);
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error during installation:[/] {ex.Message}");
            return 1;
        }
    }

    private static void InstallWindows(string exeDir)
    {
        string? currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);
        string[] paths = (currentPath ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries);

        if (paths.Contains(exeDir, StringComparer.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[yellow]Snek directory is already registered in user PATH.[/]");
            return;
        }

        string newPath = string.IsNullOrEmpty(currentPath) ? exeDir : $"{currentPath};{exeDir}";
        Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.User);

        AnsiConsole.MarkupLine("[green]Successfully added Snek directory to your User PATH![/]");
        AnsiConsole.MarkupLine("[blue]Please restart your terminal/IDE for the changes to take effect.[/]");
    }

    private static void InstallUnix(string exePath)
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string binDir = Path.Combine(userProfile, ".local", "bin");
        string linkPath = Path.Combine(binDir, "snek");

        if (!Directory.Exists(binDir))
        {
            Directory.CreateDirectory(binDir);
        }

        if (File.Exists(linkPath))
        {
            AnsiConsole.MarkupLine("[yellow]Global Snek command '~/.local/bin/snek' already exists. Recreating it...[/]");
            File.Delete(linkPath);
        }

        try
        {
            File.CreateSymbolicLink(linkPath, exePath);
            AnsiConsole.MarkupLine($"[green]Successfully symlinked Snek to '{linkPath}'![/]");
        }
        catch
        {
            File.Copy(exePath, linkPath, overwrite: true);
            AnsiConsole.MarkupLine($"[green]Successfully copied Snek to '{linkPath}'![/]");
        }

        AnsiConsole.MarkupLine("[blue]Ensure '~/.local/bin' is included in your shell's PATH variable (e.g., in ~/.bashrc or ~/.zshrc).[/]");
    }
}