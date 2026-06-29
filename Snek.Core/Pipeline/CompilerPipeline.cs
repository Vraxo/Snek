using Snek.Core.Analysis;
using Snek.Core.Ast;
using Snek.Core.Diagnoistics;
using Snek.Core.Generation;
using Snek.Core.Lexing;
using Snek.Core.Parsing;

namespace Snek.Core.Pipeline;

public class ModuleMetadata
{
    public string Name { get; }
    public Dictionary<string, bool> Declarations { get; } = []; // Name -> IsPublic

    public ModuleMetadata(string name, ProgramNode program)
    {
        Name = name;
        foreach (StatementNode statement in program.Statements)
        {
            if (statement is ClassDefNode classDef)
            {
                Declarations[classDef.Name.Value] = classDef.IsPublic;
            }
            else if (statement is FunctionDefNode func)
            {
                Declarations[func.Name.Value] = func.IsPublic;
            }
            else if (statement is ExternFunctionDefNode extFunc)
            {
                Declarations[extFunc.Name.Value] = extFunc.IsPublic;
            }
            else if (statement is ImplBlockNode implBlock)
            {
                foreach (FunctionDefNode method in implBlock.Methods)
                {
                    string methodName = $"{implBlock.TargetClass.Value}_{method.Name.Value}";
                    Declarations[methodName] = method.IsPublic;
                }
            }
        }
    }
}

public class CompilerPipeline
{
    private readonly ILexer _lexer;
    private readonly IParser _parser;
    private readonly ISemanticAnalyzer _analyzer;
    private readonly ICodeGenerator _generator;
    private readonly PipelineOptions _options;

    public CompilerPipeline(
        ILexer lexer,
        IParser parser,
        ISemanticAnalyzer analyzer,
        ICodeGenerator generator,
        PipelineOptions? options = null)
    {
        _lexer = lexer ?? throw new ArgumentNullException(nameof(lexer));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _options = options ?? new PipelineOptions();
    }

    public CompilationResult Compile(string source, string sourceName = "<input>")
    {
        CompilationContext context = new(sourceName, _options);

        try
        {
            // Stage 1: Lexing
            if (_options.EnableLogging)
            {
                LogStage(sourceName, "Lexing");
            }

            IEnumerable<Token> tokens = _lexer.Tokenize(source, context);

            // Stage 2: Parsing (Stage 1 Parsing)
            if (_options.EnableLogging)
            {
                LogStage(sourceName, "Parsing");
            }

            AstNode ast = _parser.Parse(tokens, context);
            if (context.Diagnostics.Any(d => d.IsError))
            {
                return new CompilationResult(context.Diagnostics);
            }

            // Stage 2.5: Pre-process Imports
            if (ast is ProgramNode program)
            {
                HashSet<string> importedModules = [];
                Dictionary<string, string> globalAliasTable = [];
                List<StatementNode> resolvedStatements = ResolveImportsAndBuildAliasTable(program.Statements, context, importedModules, globalAliasTable);

                // Re-lex and Parse parent file if imports changed symbol names
                if (globalAliasTable.Count > 0)
                {
                    List<Token> renamedTokens = [];
                    foreach (Token t in tokens)
                    {
                        if (t.Type == TokenType.Identifier && globalAliasTable.TryGetValue(t.Value, out string? renamed))
                        {
                            renamedTokens.Add(t with { Value = renamed });
                        }
                        else
                        {
                            renamedTokens.Add(t);
                        }
                    }

                    AstNode finalParentAst = _parser.Parse(renamedTokens, context);
                    if (finalParentAst is ProgramNode finalParentProgram)
                    {
                        List<StatementNode> parentCleanedStatements = [];
                        foreach (StatementNode s in finalParentProgram.Statements)
                        {
                            if (s is not ModuleDeclarationNode and not UseStatementNode)
                            {
                                parentCleanedStatements.Add(s);
                            }
                        }
                        resolvedStatements.AddRange(parentCleanedStatements);
                    }
                }
                else
                {
                    List<StatementNode> parentCleanedStatements = [];
                    foreach (StatementNode s in program.Statements)
                    {
                        if (s is not ModuleDeclarationNode and not UseStatementNode)
                        {
                            parentCleanedStatements.Add(s);
                        }
                    }
                    resolvedStatements.AddRange(parentCleanedStatements);
                }

                ast = new ProgramNode(resolvedStatements);
            }

            // Stage 3: Semantic Analysis
            if (_options.EnableLogging)
            {
                LogStage(sourceName, "Analyzing");
            }

            _analyzer.Analyze(ast, context);
            if (context.Diagnostics.Any(d => d.IsError))
            {
                return new CompilationResult(context.Diagnostics);
            }

            // Stage 4: Code Generation
            if (_options.EnableLogging)
            {
                LogStage(sourceName, "Generating code for");
            }

            string? output = _generator.Generate(ast, context);

            return context.Diagnostics.Any(d => d.IsError)
                ? new(context.Diagnostics)
                : new(output, context.Diagnostics);
        }
        catch (Exception ex)
        {
            context.Diagnostics.Add(new(
                sourceName,
                $"Internal compiler error: {ex.Message}",
                -1,
                -1,
                DiagnosticSeverity.Error));

            return new(context.Diagnostics);
        }
    }

    private List<StatementNode> ResolveImportsAndBuildAliasTable(
        IReadOnlyList<StatementNode> statements,
        CompilationContext context,
        HashSet<string> importedModules,
        Dictionary<string, string> parentAliasTable)
    {
        List<StatementNode> resolved = [];
        Dictionary<string, ModuleMetadata> moduleMetadataCache = [];

        // 1. Process Mod Declarations first
        foreach (StatementNode statement in statements)
        {
            if (statement is ModuleDeclarationNode modNode)
            {
                string moduleName = modNode.Name.Value;
                if (importedModules.Contains(moduleName))
                {
                    continue; // Prevent cycle/duplicate loading
                }
                importedModules.Add(moduleName);

                string? moduleSource = null;

                // Multi-path module file resolution
                string relativeDir = Path.GetDirectoryName(Path.GetFullPath(context.SourceName)) ?? Directory.GetCurrentDirectory();
                string relativePath = Path.Combine(relativeDir, $"{moduleName}.snek");
                string stdLibPath = Path.Combine(AppContext.BaseDirectory, "std", $"{moduleName}.snek");
                string cwdPath = Path.Combine(Directory.GetCurrentDirectory(), $"{moduleName}.snek");
                string devPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Snek", "std", $"{moduleName}.snek");

                if (File.Exists(relativePath))
                {
                    moduleSource = File.ReadAllText(relativePath);
                }
                else if (File.Exists(stdLibPath))
                {
                    moduleSource = File.ReadAllText(stdLibPath);
                }
                else if (File.Exists(cwdPath))
                {
                    moduleSource = File.ReadAllText(cwdPath);
                }
                else if (File.Exists(devPath))
                {
                    moduleSource = File.ReadAllText(devPath);
                }

                if (moduleSource == null)
                {
                    context.Diagnostics.Add(new(
                        context.SourceName,
                        $"Module '{moduleName}' not found",
                        modNode.Name.Line, modNode.Name.Column,
                        DiagnosticSeverity.Error));
                    continue;
                }

                // Initial Tokenization and Parse for ModuleMetadata (Visibilities)
                IEnumerable<Token> rawTokens = _lexer.Tokenize(moduleSource, context);
                AstNode rawModuleAst = _parser.Parse(rawTokens, context);

                if (rawModuleAst is ProgramNode rawModuleProgram)
                {
                    ModuleMetadata meta = new(moduleName, rawModuleProgram);
                    moduleMetadataCache[moduleName] = meta;

                    // Build Module-Internal Rename Map
                    Dictionary<string, string> internalRenameMap = [];
                    foreach (KeyValuePair<string, bool> kvp in meta.Declarations)
                    {
                        if (kvp.Value) // IsPublic == true
                        {
                            internalRenameMap[kvp.Key] = $"{moduleName}_{kvp.Key}";
                        }
                        else
                        {
                            internalRenameMap[kvp.Key] = $"_private_{moduleName}_{kvp.Key}";
                        }
                    }

                    // Rename identifiers inside the module token stream to enforce encapsulation
                    List<Token> mangledModuleTokens = [];
                    foreach (Token t in rawTokens)
                    {
                        if (t.Type == TokenType.Identifier && internalRenameMap.TryGetValue(t.Value, out string? renamed))
                        {
                            mangledModuleTokens.Add(t with { Value = renamed });
                        }
                        else
                        {
                            mangledModuleTokens.Add(t);
                        }
                    }

                    // Re-parse the fully mangled module file
                    AstNode mangledModuleAst = _parser.Parse(mangledModuleTokens, context);
                    if (mangledModuleAst is ProgramNode mangledModuleProgram)
                    {
                        // Recursively resolve imports within module files
                        List<StatementNode> resolvedMangledModuleStatements = ResolveImportsAndBuildAliasTable(
                            mangledModuleProgram.Statements, context, importedModules, parentAliasTable);

                        resolved.AddRange(resolvedMangledModuleStatements);
                    }
                }
            }
        }

        // 2. Process Use Statements next
        foreach (StatementNode statement in statements)
        {
            if (statement is UseStatementNode useNode)
            {
                string moduleName = useNode.ModuleName.Value;
                if (!moduleMetadataCache.TryGetValue(moduleName, out ModuleMetadata? meta))
                {
                    context.Diagnostics.Add(new(
                        context.SourceName,
                        $"Cannot resolve use statement; module '{moduleName}' is not loaded in this file.",
                        useNode.ModuleName.Line, useNode.ModuleName.Column,
                        DiagnosticSeverity.Error));
                    continue;
                }

                if (useNode.IsWildcard)
                {
                    foreach (KeyValuePair<string, bool> kvp in meta.Declarations)
                    {
                        if (kvp.Value) // IsPublic == true
                        {
                            parentAliasTable[kvp.Key] = $"{moduleName}_{kvp.Key}";
                        }
                    }
                }
                else if (useNode.ItemName != null)
                {
                    string itemName = useNode.ItemName.Value;
                    if (meta.Declarations.TryGetValue(itemName, out bool isPublic))
                    {
                        if (isPublic)
                        {
                            parentAliasTable[itemName] = $"{moduleName}_{itemName}";
                        }
                        else
                        {
                            context.Diagnostics.Add(new(
                                context.SourceName,
                                $"Error: member '{itemName}' is private in module '{moduleName}'",
                                useNode.ItemName.Line, useNode.ItemName.Column,
                                DiagnosticSeverity.Error));
                        }
                    }
                    else
                    {
                        context.Diagnostics.Add(new(
                            context.SourceName,
                            $"Error: module '{moduleName}' does not declare a public member '{itemName}'",
                            useNode.ItemName.Line, useNode.ItemName.Column,
                            DiagnosticSeverity.Error));
                    }
                }
            }
        }

        return resolved;
    }

    private static void LogStage(string sourceName, string stage)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("[Snek] ");
        Console.ResetColor();
        Console.WriteLine($"{stage} '{sourceName}'...");
    }
}