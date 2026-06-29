using Snek.Core.Lexing;

namespace Snek.Core.Ast;

public record ExternFunctionDefNode(Token Name, List<ParameterNode> Parameters, TypeNode? ReturnType, bool IsPublic = false) : StatementNode;