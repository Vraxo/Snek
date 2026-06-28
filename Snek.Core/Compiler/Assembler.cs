using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Snek.Core.Compiler;

public sealed class Assembler
{
    private static string? LocateExecutable(string executableName)
    {
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrEmpty(pathEnv))
        {
            return null;
        }

        char separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
        string[] directories = pathEnv.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        foreach (string directory in directories)
        {
            string fullPath = Path.Combine(directory, executableName);

            if (!File.Exists(fullPath))
            {
                continue;
            }

            return fullPath;
        }

        return null;
    }

    public static bool Assemble(string asmPath, string outputDir)
    {
        string fasmExecutableName = OperatingSystem.IsWindows() ? "fasm.exe" : "fasm";
        string? fasmPath = LocateExecutable(fasmExecutableName);

        if (fasmPath == null)
        {
            Console.Error.WriteLine($"Error: FASM executable '{fasmExecutableName}' not found in PATH.");
            Console.Error.WriteLine("Please install Flat Assembler (FASM) from https://flatassembler.net/");
            Console.Error.WriteLine("Ensure the directory containing 'fasm' is added to your PATH environment variable.");
            return false;
        }

        try
        {
            Console.WriteLine("Executing FASM assembler...");

            ProcessStartInfo startInfo = CreateProcessStartInfo(fasmPath, asmPath, outputDir);
            SetIncludeEnvironmentVariable(startInfo, fasmPath);

            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                Console.Error.WriteLine("Failed to start FASM process.");
                return false;
            }

            string output = process.StandardOutput.ReadToEnd();
            string errors = process.StandardError.ReadToEnd();
            process.WaitForExit();

            PrintOutput(output, errors);

            if (process.ExitCode == 0)
            {
                Console.WriteLine("FASM execution successful.");
                return true;
            }
            else
            {
                Console.Error.WriteLine($"FASM execution failed with exit code {process.ExitCode}.");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error executing FASM: {ex.Message}");
            return false;
        }
    }

    private static ProcessStartInfo CreateProcessStartInfo(string fasmPath, string asmPath, string outputDir)
    {
        return new()
        {
            FileName = fasmPath,
            Arguments = $"\"{Path.GetFileName(asmPath)}\"",
            WorkingDirectory = outputDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private static void SetIncludeEnvironmentVariable(ProcessStartInfo startInfo, string fasmPath)
    {
        string? fasmDirectory = Path.GetDirectoryName(fasmPath);

        if (fasmDirectory == null)
        {
            return;
        }

        string includePath = Path.Combine(fasmDirectory, "INCLUDE");

        if (Directory.Exists(includePath))
        {
            startInfo.EnvironmentVariables["INCLUDE"] = includePath;
        }
    }

    private static void PrintOutput(string output, string errors)
    {
        if (!string.IsNullOrWhiteSpace(output))
        {
            Console.Write(output);
        }

        if (!string.IsNullOrWhiteSpace(errors))
        {
            Console.Error.Write(errors);
        }
    }
}