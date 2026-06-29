using Snek.Core.Lexing;

namespace Snek.Core.Ast;

public record ModuleDeclarationNode(Token Name) : StatementNode;