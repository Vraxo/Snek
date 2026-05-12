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
        _ctx.Emit("call _main");
        _ctx.Emit("push eax");
        _ctx.Emit("call [ExitProcess]");
        _ctx.EmitLine();
    }

    private static string EncodeStringLiteral(string value)
    {
        List<string> parts = [];

        foreach (char c in value)
        {
            if (IsSpecialCharacter(c))
            {
                CloseOpenStringPart(parts);
                parts.Add(((byte)c).ToString());
            }
            else
            {
                AppendToStringPart(parts, c);
            }
        }

        CloseOpenStringPart(parts);
        parts.Add("0");

        return string.Join(",", parts);
    }

    private static bool IsSpecialCharacter(char c)
    {
        return c is '\n' or '\t' or '\r' or '\'' or '"';
    }

    private static void CloseOpenStringPart(List<string> parts)
    {
        if (parts.Count == 0 || parts[^1].EndsWith(","))
        {
            return;
        }

        parts[^1] += "'";
    }

    private static void AppendToStringPart(List<string> parts, char c)
    {
        if (parts.Count == 0 || parts[^1].EndsWith(","))
        {
            parts.Add("'");
        }

        parts[^1] += c;
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