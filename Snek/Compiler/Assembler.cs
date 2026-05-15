using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Snek.Compiler;

public class Assembler
{
    private static string? FindExecutableInPath(string executableName)
    {
        // Get PATH environment variable
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
            return null;

        // Split PATH into directories (platform-specific path separator)
        char separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
        string[] directories = pathEnv.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        // On Windows, if executable doesn't have extension, try .exe, .com, .bat, etc.
        // But we already have fasm.exe or fasm, so just check existence.
        foreach (string dir in directories)
        {
            string fullPath = Path.Combine(dir, executableName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }
        return null;
    }

    public static bool Assemble(string asmPath, string outputDir)
    {
        // Try to locate fasm executable in the system PATH
        string fasmExe = OperatingSystem.IsWindows() ? "fasm.exe" : "fasm";
        string? fasmPath = FindExecutableInPath(fasmExe);

        if (fasmPath == null)
        {
            Console.Error.WriteLine($"Error: FASM executable '{fasmExe}' not found in PATH.");
            Console.Error.WriteLine("Please install Flat Assembler (FASM) from https://flatassembler.net/");
            Console.Error.WriteLine("Ensure the directory containing 'fasm' is added to your PATH environment variable.");
            return false;
        }

        try
        {
            Console.WriteLine("Executing FASM assembler...");

            ProcessStartInfo startInfo = new()
            {
                FileName = fasmPath,
                Arguments = $"\"{Path.GetFileName(asmPath)}\"",
                WorkingDirectory = outputDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Set FASM INCLUDE path if it exists alongside fasm.exe
            string fasmInclude = Path.Combine(Path.GetDirectoryName(fasmPath) ?? "", "INCLUDE");
            if (Directory.Exists(fasmInclude))
            {
                startInfo.EnvironmentVariables["INCLUDE"] = fasmInclude;
            }

            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                Console.Error.WriteLine("Failed to start FASM process.");
                return false;
            }

            string output = process.StandardOutput.ReadToEnd();
            string errors = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(output))
            {
                Console.Write(output);
            }

            if (!string.IsNullOrWhiteSpace(errors))
            {
                Console.Error.Write(errors);
            }

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
}