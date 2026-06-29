using Snek.Core.Lexing;

namespace Snek.Core.Ast;

public record FunctionDefNode(
    Token Name,
    List<ParameterNode> Parameters,
    TypeNode? ReturnType,
    List<StatementNode> Body,
    bool IsPublic = false) : StatementNode;