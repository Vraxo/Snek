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
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("[FASM] ");
            Console.ResetColor();
            Console.WriteLine($"Assembling {Path.GetFileName(asmPath)}...");

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

            if (process.ExitCode == 0)
            {
                string stats = "";
                string[] lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    if (line.Contains("passes,") && line.Contains("bytes."))
                    {
                        stats = $" ({line.Trim()})";
                        break;
                    }
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("[FASM] ");
                Console.ResetColor();
                Console.WriteLine($"Assembly successful{stats}");
                return true;
            }
            else
            {
                FormatAndPrintFasmError(output, asmPath, outputDir);
                if (!string.IsNullOrWhiteSpace(errors))
                {
                    Console.Error.Write(errors);
                }
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error executing FASM: {ex.Message}");
            return false;
        }
    }

    private static void FormatAndPrintFasmError(string output, string asmPath, string outputDir)
    {
        string[] lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        string? errorFile = null;
        int errorLineNum = -1;
        string? errorMsg = null;
        string? faultLine = null;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            if (line.Contains("[") && line.EndsWith("]:"))
            {
                int openBracket = line.LastIndexOf('[');
                int closeBracket = line.LastIndexOf(']');
                if (openBracket != -1 && closeBracket != -1 && closeBracket > openBracket)
                {
                    errorFile = line[..openBracket].Trim();
                    string lineStr = line.Substring(openBracket + 1, closeBracket - openBracket - 1);
                    int.TryParse(lineStr, out errorLineNum);

                    if (i + 1 < lines.Length)
                    {
                        faultLine = lines[i + 1].Trim();
                    }
                }
            }
            else if (line.StartsWith("error:"))
            {
                errorMsg = line["error:".Length..].Trim();
            }
        }

        if (errorFile != null && errorLineNum > 0 && errorMsg != null)
        {
            Console.Error.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.Write("Assembler Error: ");
            Console.ResetColor();
            Console.Error.WriteLine(errorMsg);

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Error.WriteLine($"  --> {errorFile}:{errorLineNum}");
            Console.Error.WriteLine("   |");

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Error.Write($"{errorLineNum,4} | ");
            Console.ResetColor();

            string lineContent = faultLine ?? "";
            string fullPath = Path.Combine(outputDir, errorFile);
            if (File.Exists(fullPath))
            {
                try
                {
                    string[] asmLines = File.ReadAllLines(fullPath);
                    if (errorLineNum <= asmLines.Length)
                    {
                        lineContent = asmLines[errorLineNum - 1];
                    }
                }
                catch { }
            }
            Console.Error.WriteLine(lineContent);

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Error.Write("     | ");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("^");
            Console.ResetColor();
            Console.Error.WriteLine();
        }
        else
        {
            Console.Write(output);
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
}