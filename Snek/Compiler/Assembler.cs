using System.Diagnostics;

namespace Snek.Compiler;

public class Assembler
{
    public static bool Assemble(string asmPath, string outputDir)
    {
        string fasmPath = Path.Combine(AppContext.BaseDirectory, "fasm", "fasm.exe");

        if (!File.Exists(fasmPath))
        {
            Console.Error.WriteLine($"Error: FASM executable not found at '{fasmPath}'");
            Console.Error.WriteLine("Please ensure FASM is installed in the 'fasm' subdirectory.");
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