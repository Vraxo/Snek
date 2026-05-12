using System.Text;

namespace Snek.Generation;

public class GenerationContext
{
    private const string Indent = "    ";

    public StringBuilder Output { get; } = new();
    public Stack<string> LabelStack { get; } = new();
    public Dictionary<string, string> StringLiterals { get; } = [];
    public HashSet<string> ExternalFunctions { get; } = [];
    public int LabelCounter { get; set; }
    public int StringCounter { get; set; }

    public void Reset()
    {
        Output.Clear();
        LabelStack.Clear();
        StringLiterals.Clear();
        ExternalFunctions.Clear();
        LabelCounter = 0;
        StringCounter = 0;
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
}