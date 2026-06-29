using Snek.Core.Lexing;

namespace Snek.Core.Ast;

public record ImplBlockNode(Token TargetClass, List<FunctionDefNode> Methods) : StatementNode;