using Snek.Ast;
using Snek.Lexer;
using Snek.Pipeline;
using System.Text;

namespace Snek.Generation;

public class SnekCodeGenerator : ICodeGenerator
{
    private readonly StringBuilder _output = new();
    private readonly Stack<string> _labelStack = new();
    private readonly Dictionary<string, string> _stringLiterals = [];
    private readonly HashSet<string> _externalFunctions = [];
    private int _labelCounter;
    private int _stringCounter;
    private CompilationContext _context = null!;

    public string? Generate(AstNode root, CompilationContext context)
    {
        _context = context;
        _ = _output.Clear();
        _labelStack.Clear();
        _stringLiterals.Clear();
        _externalFunctions.Clear();
        _labelCounter = 0;
        _stringCounter = 0;

        if (root is not ProgramNode program)
        {
            return null;
        }

        // First pass: collect string literals and external function references
        CollectStringsAndExternals(program);

        EmitHeader();
        EmitDataSection();
        EmitImportSection();
        EmitTextSectionHeader();
        EmitEntryPoint();

        foreach (var stmt in program.Statements)
        {
            if (stmt is FunctionDefNode func)
            {
                EmitFunction(func);
            }
        }

        return _output.ToString();
    }

    private void CollectStringsAndExternals(AstNode node)
    {
        if (node is LiteralExpressionNode lit && lit.Value.Type == TokenType.StringLiteral)
        {
            if (!_stringLiterals.ContainsValue(lit.Value.Value))
            {
                _stringLiterals[$"str{_stringCounter++}"] = lit.Value.Value;
            }
        }
        else if (node is CallExpressionNode call && call.Callee is IdentifierExpressionNode id)
        {
            if (id.Name.Value is not "main" and not "print")
            {
                _ = _externalFunctions.Add(id.Name.Value);
            }
        }

        foreach (var prop in node.GetType().GetProperties())
        {
            if (prop.Name == "Parent")
            {
                continue;
            }

            var value = prop.GetValue(node);
            if (value is AstNode child)
            {
                CollectStringsAndExternals(child);
            }
            else if (value is IEnumerable<AstNode> children)
            {
                foreach (var c in children)
                {
                    CollectStringsAndExternals(c);
                }
            }
        }
    }

    private void EmitHeader()
    {
        _ = _output.AppendLine("format PE console");
        _ = _output.AppendLine("entry start");
        _ = _output.AppendLine();
        _ = _output.AppendLine("include 'win32a.inc'");
        _ = _output.AppendLine();
    }

    private void EmitDataSection()
    {
        if (_stringLiterals.Count == 0)
        {
            return;
        }

        _ = _output.AppendLine("section '.data' data readable writeable");
        foreach (var (label, value) in _stringLiterals)
        {
            _ = _output.Append($"    {label} db ");
            var parts = new List<string>();
            foreach (char c in value)
            {
                if (c is '\n' or '\t' or '\r' or '\'' or '"')
                {
                    if (parts.Count > 0 && !parts[^1].EndsWith("'"))
                    {
                        parts[^1] += "',";
                    }

                    parts.Add(((byte)c).ToString());
                }
                else
                {
                    if (parts.Count == 0 || parts[^1].StartsWith((char)(byte)0) || parts[^1].EndsWith(","))
                    {
                        parts.Add("'");
                    }

                    parts[^1] += c;
                }
            }
            if (parts.Count > 0 && !parts[^1].EndsWith("'"))
            {
                parts[^1] += "'";
            }

            parts.Add("0");
            _ = _output.AppendLine(string.Join(",", parts));
        }
        _ = _output.AppendLine();
    }

    private void EmitImportSection()
    {
        _ = _output.AppendLine("section '.idata' import data readable");
        _ = _output.AppendLine();

        var libs = new Dictionary<string, HashSet<string>>
        {
            ["kernel32.dll"] = ["ExitProcess"],
            ["msvcrt.dll"] = ["printf"]
        };

        foreach (var func in _externalFunctions)
        {
            _ = libs["msvcrt.dll"].Add(func);
        }

        var libDefs = libs.Keys.Select(lib => $"{lib.Split('.')[0]},'{lib}'");
        _ = _output.AppendLine($"    library {string.Join(",", libDefs)}");
        _ = _output.AppendLine();

        foreach (var (libName, functions) in libs.OrderBy(k => k.Key))
        {
            var alias = libName.Split('.')[0];
            var imports = functions.OrderBy(f => f).Select(f => $"{f},'{f}'");
            _ = _output.AppendLine($"    import {alias}, {string.Join(",", imports)}");
        }
        _ = _output.AppendLine();
    }

    private void EmitTextSectionHeader()
    {
        _ = _output.AppendLine("section '.text' code readable executable");
        _ = _output.AppendLine();
    }

    private void EmitEntryPoint()
    {
        _ = _output.AppendLine("start:");
        _ = _output.AppendLine("    call _main");
        _ = _output.AppendLine("    push eax");
        _ = _output.AppendLine("    call [ExitProcess]");
        _ = _output.AppendLine();
    }

    private void EmitFunction(FunctionDefNode func)
    {
        string mangledName = func.Name.Value == "main" ? "_main" : func.Name.Value;
        _ = _output.AppendLine($"{mangledName}:");
        _ = _output.AppendLine("    push ebp");
        _ = _output.AppendLine("    mov ebp, esp");

        int paramOffset = 8;
        foreach (var param in func.Parameters)
        {
            _ = _output.AppendLine($"    ; param {param.Name.Value} at [ebp+{paramOffset}]");
            paramOffset += 4;
        }

        foreach (var stmt in func.Body)
        {
            EmitStatement(stmt);
        }

        if (func.ReturnType?.Name.Value == "void")
        {
            _ = _output.AppendLine("    xor eax, eax");
        }

        _ = _output.AppendLine("    leave");
        _ = _output.AppendLine("    ret");
        _ = _output.AppendLine();
    }

    private void EmitStatement(StatementNode stmt)
    {
        switch (stmt)
        {
            case ExpressionStatementNode expr:
                EmitExpression(expr.Expression);
                _ = _output.AppendLine("    pop eax");
                break;
            case ReturnStatementNode ret:
                if (ret.Value != null)
                {
                    EmitExpression(ret.Value);
                }
                else
                {
                    _ = _output.AppendLine("    xor eax, eax");
                }
                break;
            case IfStatementNode ifs:
                EmitIf(ifs);
                break;
            case WhileStatementNode whl:
                EmitWhile(whl);
                break;
        }
    }

    private void EmitIf(IfStatementNode ifs)
    {
        var elseLabel = $"_else_{_labelCounter++}";
        var endLabel = $"_endif_{_labelCounter}";

        EmitExpression(ifs.Condition);
        _ = _output.AppendLine("    pop eax");
        _ = _output.AppendLine("    test eax, eax");
        _ = _output.AppendLine($"    jz {elseLabel}");

        foreach (var s in ifs.ThenBody)
        {
            EmitStatement(s);
        }

        _ = _output.AppendLine($"    jmp {endLabel}");
        _ = _output.AppendLine($"{elseLabel}:");
        if (ifs.ElseBody != null)
        {
            foreach (var s in ifs.ElseBody)
            {
                EmitStatement(s);
            }
        }

        _ = _output.AppendLine($"{endLabel}:");
    }

    private void EmitWhile(WhileStatementNode whl)
    {
        var startLabel = $"_while_{_labelCounter}";
        var endLabel = $"_endwhile_{_labelCounter++}";

        _ = _output.AppendLine($"{startLabel}:");
        EmitExpression(whl.Condition);
        _ = _output.AppendLine("    pop eax");
        _ = _output.AppendLine("    test eax, eax");
        _ = _output.AppendLine($"    jz {endLabel}");

        foreach (var s in whl.Body)
        {
            EmitStatement(s);
        }

        _ = _output.AppendLine($"    jmp {startLabel}");
        _ = _output.AppendLine($"{endLabel}:");
    }

    private void EmitExpression(ExpressionNode expr)
    {
        switch (expr)
        {
            case LiteralExpressionNode lit:
                EmitLiteral(lit);
                break;
            case IdentifierExpressionNode id:
                _ = _output.AppendLine($"    ; load {id.Name.Value}");
                _ = _output.AppendLine("    push 0");
                break;
            case CallExpressionNode call:
                EmitCall(call);
                break;
            case BinaryExpressionNode bin:
                EmitBinary(bin);
                break;
        }
    }

    private void EmitLiteral(LiteralExpressionNode lit)
    {
        switch (lit.Value.Type)
        {
            case TokenType.StringLiteral:
                var label = _stringLiterals.First(kvp => kvp.Value == lit.Value.Value).Key;
                _ = _output.AppendLine($"    push {label}");
                break;
            case TokenType.IntegerLiteral:
                _ = _output.AppendLine($"    push {lit.Value.Value}");
                break;
            case TokenType.KeywordTrue:
                _ = _output.AppendLine("    push 1");
                break;
            case TokenType.KeywordFalse:
                _ = _output.AppendLine("    push 0");
                break;
        }
    }

    private void EmitCall(CallExpressionNode call)
    {
        for (int i = call.Arguments.Count - 1; i >= 0; i--)
        {
            EmitExpression(call.Arguments[i]);
        }

        var callee = call.Callee is IdentifierExpressionNode id ? id.Name.Value : "unknown";
        string target;

        if (callee == "print")
        {
            target = "[printf]";
            _ = _externalFunctions.Add("printf");
        }
        else if (callee == "main")
        {
            target = "_main";
        }
        else
        {
            target = _externalFunctions.Contains(callee) ? $"[{callee}]" : callee;
        }

        _ = _output.AppendLine($"    call {target}");

        if (call.Arguments.Count > 0)
        {
            _ = _output.AppendLine($"    add esp, {call.Arguments.Count * 4}");
        }

        _ = _output.AppendLine("    push eax");
    }

    private void EmitBinary(BinaryExpressionNode bin)
    {
        EmitExpression(bin.Right);
        EmitExpression(bin.Left);
        _ = _output.AppendLine("    pop ebx");
        _ = _output.AppendLine("    pop eax");

        switch (bin.Operator.Type)
        {
            case TokenType.Plus:
                _ = _output.AppendLine("    add eax, ebx");
                break;
            case TokenType.Minus:
                _ = _output.AppendLine("    sub eax, ebx");
                break;
            case TokenType.Star:
                _ = _output.AppendLine("    imul eax, ebx");
                break;
            case TokenType.DoubleEquals:
                _ = _output.AppendLine("    cmp eax, ebx");
                _ = _output.AppendLine("    sete al");
                _ = _output.AppendLine("    movzx eax, al");
                break;
            default:
                _ = _output.AppendLine("    ; unsupported binary op");
                break;
        }
        _ = _output.AppendLine("    push eax");
    }
}