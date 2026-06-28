using System.Text;

namespace Snek.Core.Generation;

public class GenerationContext
{
    private const string Indent = "    ";

    private static readonly HashSet<string> X86Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        // General-purpose instructions
        "mov", "push", "pop", "lea", "xchg", "nop",

        // Integer arithmetic
        "add", "adc", "sub", "sbb", "inc", "dec", "neg",
        "mul", "imul", "div", "idiv",
        
        // Bitwise / logical
        "and", "or", "xor", "not", "test",
        
        // Shift / rotate
        "shl", "shr", "sal", "sar", "rol", "ror", "rcl", "rcr",
        
        // Comparison
        "cmp",
        
        // Control flow
        "jmp", "call", "ret", "retn", "retf", "int", "iret", "loop", "pause",
        "je", "jne", "jz", "jnz", "ja", "jb", "jg", "jl", "jge", "jle",
        
        // Conditional set
        "sete", "setne", "setz", "setnz",
        
        // Data movement
        "cbw", "cwd", "cdq", "cwde", "movsx", "movzx",
        
        // Stack
        "pusha", "popa", "pushad", "popad", "pushf", "popf",
        "enter", "leave",
        
        // String
        "movs", "cmps", "scas", "lods", "stos",
        
        // FASM directives that clash
        "format", "entry", "include", "section",
        "library", "import", "align",
        "db", "dw", "dd", "dq", "dt", "rb", "rw", "rd", "rq",
    };

    public StringBuilder Output { get; } = new();
    public Stack<string> LabelStack { get; } = new();
    public Dictionary<string, string> StringLiterals { get; } = [];
    public HashSet<string> ExternalFunctions { get; } = [];
    public int LabelCounter { get; set; }
    public int StringCounter { get; set; }
    public Dictionary<string, int> LocalOffsets { get; } = [];
    public int NextLocalOffset { get; set; } = 4;

    public void Reset()
    {
        Output.Clear();
        LabelStack.Clear();
        StringLiterals.Clear();
        ExternalFunctions.Clear();
        LabelCounter = 0;
        StringCounter = 0;
        LocalOffsets.Clear();
        NextLocalOffset = 4;
    }

    public string MangleName(string name)
    {
        return X86Reserved.Contains(name)
            ? $"_{name}"
            : name;
    }

    public void Emit(string instruction)
    {
        Output.Append(Indent);
        Output.AppendLine(instruction);
    }

    public void EmitLine(string text = "")
    {
        Output.AppendLine(text);
    }

    public string EnsureFormatString(string format)
    {
        foreach (KeyValuePair<string, string> kvp in StringLiterals)
        {
            if (kvp.Value == format)
            {
                return kvp.Key;
            }
        }

        string label = $"fmt{StringCounter++}";
        StringLiterals[label] = format;
        return label;
    }
}