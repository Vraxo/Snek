using System.Text;

namespace Snek.Generation;

public class SectionEmitter
{
    private readonly GenerationContext _ctx;

    public SectionEmitter(GenerationContext ctx)
    {
        _ctx = ctx;
    }

    public void EmitHeader()
    {
        _ctx.EmitLine("format PE console");
        _ctx.EmitLine("entry start");
        _ctx.EmitLine();
        _ctx.EmitLine("include 'win32a.inc'");
        _ctx.EmitLine();
    }

    public void EmitDataSection()
    {
        if (_ctx.StringLiterals.Count == 0)
        {
            return;
        }

        _ctx.EmitLine("section '.data' data readable writeable");

        foreach ((string? label, string? value) in _ctx.StringLiterals)
        {
            string encoded = EncodeStringLiteral(value);
            _ctx.Emit($"{label} db {encoded}");
        }

        _ctx.EmitLine();
    }

    public void EmitImportSection()
    {
        _ctx.EmitLine("section '.idata' import data readable");
        _ctx.EmitLine();

        Dictionary<string, HashSet<string>> libs = BuildImportLibrary();

        _ctx.Emit($"library {FormatLibraryDefinitions(libs)}");
        _ctx.EmitLine();

        foreach ((string? libName, HashSet<string>? functions) in libs.OrderBy(k => k.Key))
        {
            _ctx.Emit($"import {FormatImportLine(libName, functions)}");
        }

        _ctx.EmitLine();
    }

    public void EmitTextSectionHeader()
    {
        _ctx.EmitLine("section '.text' code readable executable");
        _ctx.EmitLine();
    }

    public void EmitEntryPoint()
    {
        _ctx.EmitLine("start:");
        _ctx.Emit("call _start");
        _ctx.Emit("push eax");
        _ctx.Emit("call [ExitProcess]");
        _ctx.EmitLine();
    }

    private static string EncodeStringLiteral(string value)
    {
        var parts = new List<string>();
        bool inQuoted = false;

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (NeedsByteEncoding(c))
            {
                // Close any open quoted segment
                if (inQuoted)
                {
                    parts[^1] += "'";
                    inQuoted = false;
                }
                // Add the numeric byte value
                parts.Add(((byte)c).ToString());
            }
            else
            {
                if (!inQuoted)
                {
                    parts.Add("'");
                    inQuoted = true;
                }
                parts[^1] += c;
            }
        }

        // Close final quoted segment if open
        if (inQuoted)
        {
            parts[^1] += "'";
        }

        // Add null terminator
        parts.Add("0");

        return string.Join(",", parts);
    }

    private static bool NeedsByteEncoding(char c)
    {
        // Characters that must be encoded as numeric bytes: newline, tab, carriage return, quote, backslash, etc.
        return c is '\n' or '\t' or '\r' or '\'' or '"' or '\\';
    }

    private Dictionary<string, HashSet<string>> BuildImportLibrary()
    {
        Dictionary<string, HashSet<string>> libs = new()
        {
            ["kernel32.dll"] = ["ExitProcess"],
            ["msvcrt.dll"] = ["printf"]
        };

        foreach (string func in _ctx.ExternalFunctions)
        {
            libs["msvcrt.dll"].Add(func);
        }

        return libs;
    }

    private static string FormatLibraryDefinitions(Dictionary<string, HashSet<string>> libs)
    {
        IEnumerable<string> defs = libs.Keys
            .Select(lib => $"{lib.Split('.')[0]},'{lib}'");

        return string.Join(",", defs);
    }

    private static string FormatImportLine(string libName, HashSet<string> functions)
    {
        string alias = libName.Split('.')[0];

        IEnumerable<string> imports = functions
            .OrderBy(f => f)
            .Select(f => $"{f},'{f}'");

        return $"{alias}, {string.Join(",", imports)}";
    }
}