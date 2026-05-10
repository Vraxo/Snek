using Snek.Lexer;

namespace Snek.Ast;

public record FunctionDefNode(
    Token Name,
    List<ParameterNode> Parameters,
    TypeNode? ReturnType,
    List<StatementNode> Body,
    int IndentLevel) : StatementNode;
