using Snek.Ast;
using Snek.Lexer;
using Snek.Pipeline;
using System.Reflection;

namespace Snek.Generation;

public class CodeGenerator : ICodeGenerator
{
    private readonly GenerationContext _ctx = new();
    private ExpressionEmitter _expressions = null!;
    private StatementEmitter _statements = null!;
    private CompilationContext _compilationContext = null!;

    public string? Generate(AstNode root, CompilationContext context)
    {
        _compilationContext = context;
        _ctx.Reset();
        _expressions = new(_ctx);
        _statements = new(_ctx, _expressions);

        if (root is not ProgramNode program)
        {
            return null;
        }

        CollectStringsAndExternals(program);

        EmitHeader();
        EmitDataSection();
        EmitImportSection();
        EmitTextSectionHeader();
        EmitEntryPoint();

        foreach (StatementNode stmt in program.Statements)
        {
            if (stmt is not FunctionDefNode func)
            {
                continue;
            }

            EmitFunction(func);
        }

        return _ctx.Output.ToString();
    }

    private void CollectStringsAndExternals(AstNode node)
    {
        if (node is LiteralExpressionNode lit && lit.Value.Type == TokenType.StringLiteral)
        {
            if (!_ctx.StringLiterals.ContainsValue(lit.Value.Value))
            {
                _ctx.StringLiterals[$"str{_ctx.StringCounter++}"] = lit.Value.Value;
            }
        }
        else if (node is CallExpressionNode call && call.Callee is IdentifierExpressionNode id)
        {
            if (id.Name.Value is not "main" and not "print")
            {
                _ctx.ExternalFunctions.Add(id.Name.Value);
            }
        }

        foreach (PropertyInfo prop in node.GetType().GetProperties())
        {
            if (prop.Name == "Parent")
            {
                continue;
            }

            object? value = prop.GetValue(node);

            if (value is AstNode child)
            {
                CollectStringsAndExternals(child);
            }
            else if (value is IEnumerable<AstNode> children)
            {
                foreach (AstNode c in children)
                {
                    CollectStringsAndExternals(c);
                }
            }
        }
    }

    private void EmitHeader()
    {
        _ctx.Output.AppendLine("format PE console");
        _ctx.Output.AppendLine("entry start");
        _ctx.Output.AppendLine();
        _ctx.Output.AppendLine("include 'win32a.inc'");
        _ctx.Output.AppendLine();
    }

    private void EmitDataSection()
    {
        if (_ctx.StringLiterals.Count == 0)
        {
            return;
        }

        _ctx.Output.AppendLine("section '.data' data readable writeable");

        foreach ((string? label, string? value) in _ctx.StringLiterals)
        {
            _ctx.Output.Append($"    {label} db ");
            List<string> parts = [];

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
            _ctx.Output.AppendLine(string.Join(",", parts));
        }

        _ctx.Output.AppendLine();
    }

    private void EmitImportSection()
    {
        _ctx.Output.AppendLine("section '.idata' import data readable");
        _ctx.Output.AppendLine();

        Dictionary<string, HashSet<string>> libs = new()
        {
            ["kernel32.dll"] = ["ExitProcess"],
            ["msvcrt.dll"] = ["printf"]
        };

        foreach (string func in _ctx.ExternalFunctions)
        {
            libs["msvcrt.dll"].Add(func);
        }

        IEnumerable<string> libDefs = libs.Keys
            .Select(lib => $"{lib.Split('.')[0]},'{lib}'");

        _ctx.Output.AppendLine($"    library {string.Join(",", libDefs)}");
        _ctx.Output.AppendLine();

        foreach ((string? libName, HashSet<string>? functions) in libs.OrderBy(k => k.Key))
        {
            string alias = libName.Split('.')[0];

            IEnumerable<string> imports = functions
                .OrderBy(f => f)
                .Select(f => $"{f},'{f}'");

            _ctx.Output.AppendLine($"    import {alias}, {string.Join(",", imports)}");
        }

        _ctx.Output.AppendLine();
    }

    private void EmitTextSectionHeader()
    {
        _ctx.Output.AppendLine("section '.text' code readable executable");
        _ctx.Output.AppendLine();
    }

    private void EmitEntryPoint()
    {
        _ctx.Output.AppendLine("start:");
        _ctx.Output.AppendLine("    call _main");
        _ctx.Output.AppendLine("    push eax");
        _ctx.Output.AppendLine("    call [ExitProcess]");
        _ctx.Output.AppendLine();
    }

    private void EmitFunction(FunctionDefNode func)
    {
        string mangledName = func.Name.Value == "main"
            ? "_main"
            : func.Name.Value;

        _ctx.Output.AppendLine($"{mangledName}:");
        _ctx.Output.AppendLine("    push ebp");
        _ctx.Output.AppendLine("    mov ebp, esp");

        int paramOffset = 8;

        foreach (ParameterNode param in func.Parameters)
        {
            _ctx.Output.AppendLine($"    ; param {param.Name.Value} at [ebp+{paramOffset}]");
            paramOffset += 4;
        }

        foreach (StatementNode stmt in func.Body)
        {
            _statements.Emit(stmt);
        }

        if (func.ReturnType?.Name.Value == "void")
        {
            _ctx.Output.AppendLine("    xor eax, eax");
        }

        _ctx.Output.AppendLine("    leave");
        _ctx.Output.AppendLine("    ret");
        _ctx.Output.AppendLine();
    }
}