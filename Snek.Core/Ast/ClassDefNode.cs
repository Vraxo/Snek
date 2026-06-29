using Snek.Core.Lexing;

namespace Snek.Core.Ast;

public record ClassDefNode(Token Name, List<FieldNode> Fields) : StatementNode;