using Snek.Core.Ast;

namespace Snek.Core.Generation;

public class StatementEmitter
{
    private readonly GenerationContext _generationContext;
    private readonly ExpressionEmitter _expressionEmitter;

    public StatementEmitter(GenerationContext generationContext, ExpressionEmitter expressionEmitter)
    {
        _generationContext = generationContext;
        _expressionEmitter = expressionEmitter;
    }

    public void EmitEntryPoint(IReadOnlyList<StatementNode> statements)
    {
        List<StatementNode> topLevelStatements = statements
            .Where(statement => statement is not FunctionDefNode and not ExternFunctionDefNode and not ClassDefNode and not ImplBlockNode)
            .ToList();

        _generationContext.EmitLine("_start:");

        if (topLevelStatements.Count == 0)
        {
            _generationContext.Emit("xor eax, eax");
            _generationContext.Emit("ret");
            _generationContext.EmitLine();
            return;
        }

        BeginFunctionPrologue();
        ReserveLocalVariablesSpace(topLevelStatements);
        EmitStatements(topLevelStatements);
        EmitFunctionEpilogueWithZeroReturn();
    }

    public void Emit(StatementNode statement)
    {
        switch (statement)
        {
            case ExpressionStatementNode expressionStatement:
                EmitExpressionStatement(expressionStatement);
                break;
            case ReturnStatementNode returnStatement:
                EmitReturnStatement(returnStatement);
                break;
            case IfStatementNode ifStatement:
                EmitIfStatement(ifStatement);
                break;
            case WhileStatementNode whileStatement:
                EmitWhileStatement(whileStatement);
                break;
            case VariableDeclarationNode variableDeclaration:
                EmitVariableDeclaration(variableDeclaration);
                break;
            case AssignmentStatementNode assignment:
                EmitAssignmentStatement(assignment);
                break;
            default:
                _generationContext.Emit("; unsupported statement");
                break;
        }
    }

    public void EmitFunction(FunctionDefNode function)
    {
        // 1. Save parent scope state
        Dictionary<string, int> oldLocalOffsets = new(_generationContext.LocalOffsets);
        int oldNextOffset = _generationContext.NextLocalOffset;
        Dictionary<string, string> oldVariableTypes = new(_generationContext.VariableTypes);

        // 2. Reset scope context for this function
        _generationContext.LocalOffsets.Clear();
        _generationContext.NextLocalOffset = 4;
        _generationContext.VariableTypes.Clear();

        string mangledName = _generationContext.MangleName(function.Name.Value);
        _generationContext.EmitLine($"{mangledName}:");

        // Map parameter stack offsets for standard functions
        _generationContext.ParameterOffsets.Clear();
        int paramOffset = 8;
        foreach (ParameterNode parameter in function.Parameters)
        {
            _generationContext.ParameterOffsets[parameter.Name.Value] = paramOffset;
            if (parameter.TypeAnnotation != null)
            {
                _generationContext.VariableTypes[parameter.Name.Value] = parameter.TypeAnnotation.Name.Value;
            }
            paramOffset += 4;
        }

        int localsSize = ComputeLocalVariablesSize(function.Body);

        BeginFunctionPrologue();
        if (localsSize > 0)
        {
            _generationContext.Emit($"sub esp, {localsSize}");
        }

        EmitParameterComments(function.Parameters);
        EmitStatements(function.Body);

        if (function.ReturnType == null)
        {
            _generationContext.Emit("xor eax, eax");
        }

        EmitFunctionEpilogue();

        // 3. Restore parent scope state
        _generationContext.LocalOffsets.Clear();
        foreach (KeyValuePair<string, int> kvp in oldLocalOffsets)
        {
            _generationContext.LocalOffsets[kvp.Key] = kvp.Value;
        }
        _generationContext.NextLocalOffset = oldNextOffset;
        _generationContext.VariableTypes.Clear();
        foreach (KeyValuePair<string, string> kvp in oldVariableTypes)
        {
            _generationContext.VariableTypes[kvp.Key] = kvp.Value;
        }
    }

    public void EmitImplBlock(ImplBlockNode implBlock)
    {
        string className = implBlock.TargetClass.Value;
        foreach (FunctionDefNode method in implBlock.Methods)
        {
            // 1. Save parent scope state
            Dictionary<string, int> oldLocalOffsets = new(_generationContext.LocalOffsets);
            int oldNextOffset = _generationContext.NextLocalOffset;
            Dictionary<string, string> oldVariableTypes = new(_generationContext.VariableTypes);

            // 2. Reset scope context for this method
            _generationContext.LocalOffsets.Clear();
            _generationContext.NextLocalOffset = 4;
            _generationContext.VariableTypes.Clear();

            string mangledName = $"{className}_{method.Name.Value}";
            _generationContext.EmitLine($"{mangledName}:");

            _generationContext.VariableTypes["self"] = className;

            // Map parameter stack offsets dynamically based on whether it is static or an instance method
            _generationContext.ParameterOffsets.Clear();

            bool hasSelfParam = method.Parameters.Any(p => p.Name.Value == "self");
            int paramOffset = 8;
            if (hasSelfParam)
            {
                _generationContext.ParameterOffsets["self"] = 8; // 'self' is always the first parameter
                paramOffset = 12; // Subsequent parameters start at [ebp+12]
            }
            else
            {
                paramOffset = 8; // Static methods parameters start at [ebp+8]
            }

            foreach (ParameterNode parameter in method.Parameters)
            {
                if (parameter.Name.Value == "self")
                {
                    continue;
                }
                _generationContext.ParameterOffsets[parameter.Name.Value] = paramOffset;
                if (parameter.TypeAnnotation != null)
                {
                    _generationContext.VariableTypes[parameter.Name.Value] = parameter.TypeAnnotation.Name.Value;
                }
                paramOffset += 4;
            }

            int localsSize = ComputeLocalVariablesSize(method.Body);

            BeginFunctionPrologue();
            if (localsSize > 0)
            {
                _generationContext.Emit($"sub esp, {localsSize}");
            }

            EmitParameterComments(method.Parameters);
            EmitStatements(method.Body);

            if (method.ReturnType == null)
            {
                _generationContext.Emit("xor eax, eax");
            }

            EmitFunctionEpilogue();

            // 3. Restore parent scope state
            _generationContext.LocalOffsets.Clear();
            foreach (KeyValuePair<string, int> kvp in oldLocalOffsets)
            {
                _generationContext.LocalOffsets[kvp.Key] = kvp.Value;
            }
            _generationContext.NextLocalOffset = oldNextOffset;
            _generationContext.VariableTypes.Clear();
            foreach (KeyValuePair<string, string> kvp in oldVariableTypes)
            {
                _generationContext.VariableTypes[kvp.Key] = kvp.Value;
            }
        }
    }

    private void BeginFunctionPrologue()
    {
        _generationContext.Emit("push ebp");
        _generationContext.Emit("mov ebp, esp");
    }

    private void EmitFunctionEpilogueWithZeroReturn()
    {
        _generationContext.Emit("xor eax, eax");
        EmitFunctionEpilogue();
    }

    private void EmitFunctionEpilogue()
    {
        _generationContext.Emit("leave");
        _generationContext.Emit("ret");
        _generationContext.EmitLine();
    }

    private void ReserveLocalVariablesSpace(List<StatementNode> topLevelStatements)
    {
        int localsSize = ComputeLocalVariablesSize(topLevelStatements);
        if (localsSize > 0)
        {
            _generationContext.Emit($"sub esp, {localsSize}");
        }
    }

    private int ComputeLocalVariablesSize(IEnumerable<StatementNode> statements)
    {
        int size = 0;
        foreach (StatementNode statement in statements)
        {
            if (statement is VariableDeclarationNode)
            {
                size += 4;
            }
        }
        return size;
    }

    private void EmitStatements(IEnumerable<StatementNode> statements)
    {
        foreach (StatementNode statement in statements)
        {
            Emit(statement);
        }
    }

    private void EmitParameterComments(IEnumerable<ParameterNode> parameters)
    {
        int paramOffset = 8;
        foreach (ParameterNode parameter in parameters)
        {
            _generationContext.Emit($"; param {parameter.Name.Value} at [ebp+{paramOffset}]");
            paramOffset += 4;
        }
    }

    private void EmitExpressionStatement(ExpressionStatementNode expressionStatement)
    {
        _expressionEmitter.Emit(expressionStatement.Expression);
        _generationContext.Emit("pop eax");
    }

    private void EmitReturnStatement(ReturnStatementNode returnStatement)
    {
        if (returnStatement.Value == null)
        {
            _generationContext.Emit("xor eax, eax");
        }
        else
        {
            _expressionEmitter.Emit(returnStatement.Value);
        }
    }

    private void EmitIfStatement(IfStatementNode ifStatement)
    {
        string elseLabel = $"_else_{_generationContext.LabelCounter++}";
        string endLabel = $"_endif_{_generationContext.LabelCounter}";

        _expressionEmitter.Emit(ifStatement.Condition);
        _generationContext.Emit("pop eax");
        _generationContext.Emit("test eax, eax");
        _generationContext.Emit($"jz {elseLabel}");

        EmitStatements(ifStatement.ThenBody);
        _generationContext.Emit($"jmp {endLabel}");
        _generationContext.EmitLine($"{elseLabel}:");

        if (ifStatement.ElseBody != null)
        {
            EmitStatements(ifStatement.ElseBody);
        }

        _generationContext.EmitLine($"{endLabel}:");
    }

    private void EmitWhileStatement(WhileStatementNode whileStatement)
    {
        string startLabel = $"_while_{_generationContext.LabelCounter}";
        string endLabel = $"_endwhile_{_generationContext.LabelCounter++}";

        _generationContext.EmitLine($"{startLabel}:");
        _expressionEmitter.Emit(whileStatement.Condition);
        _generationContext.Emit("pop eax");
        _generationContext.Emit("test eax, eax");
        _generationContext.Emit($"jz {endLabel}");

        EmitStatements(whileStatement.Body);
        _generationContext.Emit($"jmp {startLabel}");
        _generationContext.EmitLine($"{endLabel}:");
    }

    private void EmitVariableDeclaration(VariableDeclarationNode variableDeclaration)
    {
        if (variableDeclaration.Initializer != null)
        {
            _expressionEmitter.Emit(variableDeclaration.Initializer);
        }
        else
        {
            _generationContext.Emit("xor eax, eax");
            _generationContext.Emit("push eax");
        }

        int offset = _generationContext.NextLocalOffset;
        _generationContext.LocalOffsets[variableDeclaration.Name.Value] = offset;
        _generationContext.NextLocalOffset += 4;

        _generationContext.VariableTypes[variableDeclaration.Name.Value] = variableDeclaration.Type.Name.Value;

        _generationContext.Emit("pop eax");
        _generationContext.Emit($"mov [ebp-{offset}], eax");
    }

    private void EmitAssignmentStatement(AssignmentStatementNode assignment)
    {
        _expressionEmitter.Emit(assignment.Value);

        if (assignment.Target is IdentifierExpressionNode id)
        {
            if (_generationContext.LocalOffsets.TryGetValue(id.Name.Value, out int offset))
            {
                _generationContext.Emit("pop eax");
                _generationContext.Emit($"mov [ebp-{offset}], eax");
            }
            else if (_generationContext.ParameterOffsets.TryGetValue(id.Name.Value, out int paramOffset))
            {
                _generationContext.Emit("pop eax");
                _generationContext.Emit($"mov [ebp+{paramOffset}], eax");
            }
            else
            {
                _generationContext.Emit("; undefined global assignment fallback");
                _generationContext.Emit("pop eax");
            }
        }
        else if (assignment.Target is MemberAccessExpressionNode member)
        {
            if (member.Object is IdentifierExpressionNode objId &&
                _generationContext.VariableTypes.TryGetValue(objId.Name.Value, out string? className) &&
                _generationContext.ClassFields.TryGetValue(className, out List<string>? fields))
            {
                int fieldIndex = fields.IndexOf(member.Member.Value);
                if (fieldIndex != -1)
                {
                    _expressionEmitter.Emit(member.Object); // evaluates the object pointer (pushes base address)
                    _generationContext.Emit("pop edx"); // edx = object base address pointer
                    _generationContext.Emit("pop eax"); // eax = value to assign
                    _generationContext.Emit($"mov [edx + {fieldIndex * 4}], eax"); // write value directly to field offset!
                }
                else
                {
                    _generationContext.Emit("; field not found");
                    _generationContext.Emit("pop eax");
                }
            }
            else
            {
                _generationContext.Emit("; unsupported member object assignment");
                _generationContext.Emit("pop eax");
            }
        }
        else if (assignment.Target is IndexExpressionNode index)
        {
            _expressionEmitter.Emit(index.Target);     // evaluates base address pointer
            _expressionEmitter.Emit(index.Index);      // evaluates index

            _generationContext.Emit("pop ecx"); // ecx = index
            _generationContext.Emit("pop edx"); // edx = base address pointer
            _generationContext.Emit("pop eax"); // eax = value to assign

            bool isHighLevelList = false;
            if (index.Target is IdentifierExpressionNode targetId &&
                _generationContext.VariableTypes.TryGetValue(targetId.Name.Value, out string? className) &&
                className == "List" &&
                !_generationContext.ClassFields.ContainsKey("List"))
            {
                isHighLevelList = true;
            }

            if (isHighLevelList)
            {
                _generationContext.Emit("mov [edx + ecx*4 + 4], eax");
            }
            else
            {
                _generationContext.Emit("mov [edx + ecx*4], eax");
            }
        }
        else
        {
            _generationContext.Emit("; unsupported assignment target");
            _generationContext.Emit("pop eax");
        }
    }
}