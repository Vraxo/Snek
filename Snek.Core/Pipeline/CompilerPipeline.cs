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
            // Exempt ExternFunctionDefNode to prevent system DLL import symbol corruption
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

            // Pre-process DoubleColon tokens in expression contexts before parsing
            tokens = MergeDoubleColonIdentifiers(tokens);

            // Stage 2: Parsing
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
                Dictionary<string, ModuleMetadata> moduleMetadataCache = [];

                List<StatementNode> resolvedStatements = ResolveImportsAndBuildAliasTable(
                    program.Statements, context, importedModules, globalAliasTable, moduleMetadataCache);

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

    private List<Token> MergeDoubleColonIdentifiers(IEnumerable<Token> tokens)
    {
        List<Token> raw = [.. tokens];
        List<Token> merged = [];
        bool inUseStatement = false;

        for (int i = 0; i < raw.Count; i++)
        {
            Token t = raw[i];

            if (t.Type == TokenType.KeywordUse)
            {
                inUseStatement = true;
            }
            else if (t.Type == TokenType.Semicolon)
            {
                inUseStatement = false;
            }

            if (!inUseStatement &&
                t.Type == TokenType.Identifier &&
                i + 2 < raw.Count &&
                raw[i + 1].Type == TokenType.DoubleColon &&
                raw[i + 2].Type == TokenType.Identifier)
            {
                Token nextIdent = raw[i + 2];
                string mergedValue = $"{t.Value}_{nextIdent.Value}";
                merged.Add(t with { Value = mergedValue });
                i += 2; // Skip DoubleColon and second Identifier
            }
            else
            {
                merged.Add(t);
            }
        }
        return merged;
    }

    private List<StatementNode> ResolveImportsAndBuildAliasTable(
        IReadOnlyList<StatementNode> statements,
        CompilationContext context,
        HashSet<string> importedModules,
        Dictionary<string, string> parentAliasTable,
        Dictionary<string, ModuleMetadata> moduleMetadataCache)
    {
        List<StatementNode> resolved = [];

        // 1. Process Mod Declarations first (for local modules)
        foreach (StatementNode statement in statements)
        {
            if (statement is ModuleDeclarationNode modNode)
            {
                string moduleName = modNode.Name.Value;
                if (importedModules.Contains(moduleName))
                {
                    continue; // Prevent infinite import loops/duplicates
                }
                importedModules.Add(moduleName);

                LoadAndMangleModule(moduleName, modNode.Name.Line, modNode.Name.Column, context, importedModules, moduleMetadataCache, resolved);
            }
        }

        // 2. Process Use Statements next
        foreach (StatementNode statement in statements)
        {
            if (statement is UseStatementNode useNode)
            {
                if (useNode.Path.Count < 2)
                {
                    context.Diagnostics.Add(new(
                        context.SourceName,
                        "Error: 'use' statement path must contain at least a module and an item",
                        useNode.Path[0].Line, useNode.Path[0].Column,
                        DiagnosticSeverity.Error));
                    continue;
                }

                // Check if the path begins with implicitly linked std package
                string packageOrModule = useNode.Path[0].Value;

                string moduleName;
                string itemName = "";

                if (packageOrModule == "std")
                {
                    if (useNode.Path.Count < 3 && !useNode.IsWildcard)
                    {
                        context.Diagnostics.Add(new(
                            context.SourceName,
                            "Error: 'use std::...' imports require a module and item name",
                            useNode.Path[0].Line, useNode.Path[0].Column,
                            DiagnosticSeverity.Error));
                        continue;
                    }

                    string stdModuleName = useNode.Path[1].Value;
                    moduleName = $"std_{stdModuleName}";

                    if (!useNode.IsWildcard)
                    {
                        itemName = useNode.Path[2].Value;
                    }

                    if (!importedModules.Contains(moduleName))
                    {
                        importedModules.Add(moduleName);
                        // Load the std library file dynamically
                        LoadAndMangleModule(stdModuleName, useNode.Path[0].Line, useNode.Path[0].Column, context, importedModules, moduleMetadataCache, resolved, isStd: true);
                    }
                }
                else
                {
                    // Local submodule use
                    moduleName = packageOrModule;
                    if (!useNode.IsWildcard)
                    {
                        itemName = useNode.Path[1].Value;
                    }
                }

                if (!moduleMetadataCache.TryGetValue(moduleName, out ModuleMetadata? meta))
                {
                    context.Diagnostics.Add(new(
                        context.SourceName,
                        $"Cannot resolve use statement; module '{moduleName}' is not loaded or declared.",
                        useNode.Path[0].Line, useNode.Path[0].Column,
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
                else
                {
                    if (meta.Declarations.TryGetValue(itemName, out bool isPublic))
                    {
                        if (isPublic)
                        {
                            parentAliasTable[itemName] = $"{moduleName}_{itemName}";

                            // Automatically import all public associated methods of this class so users don't have to import them manually!
                            foreach (KeyValuePair<string, bool> kvp in meta.Declarations)
                            {
                                if (kvp.Key.StartsWith($"{itemName}_") && kvp.Value)
                                {
                                    parentAliasTable[kvp.Key] = $"{moduleName}_{kvp.Key}";
                                }
                            }
                        }
                        else
                        {
                            context.Diagnostics.Add(new(
                                context.SourceName,
                                $"Error: member '{itemName}' is private in module '{moduleName}'",
                                useNode.Path[^1].Line, useNode.Path[^1].Column,
                                DiagnosticSeverity.Error));
                        }
                    }
                    else
                    {
                        context.Diagnostics.Add(new(
                            context.SourceName,
                            $"Error: module '{moduleName}' does not declare a public member '{itemName}'",
                            useNode.Path[^1].Line, useNode.Path[^1].Column,
                            DiagnosticSeverity.Error));
                    }
                }
            }
        }

        return resolved;
    }

    private void LoadAndMangleModule(
        string moduleName,
        int line,
        int col,
        CompilationContext context,
        HashSet<string> importedModules,
        Dictionary<string, ModuleMetadata> moduleMetadataCache,
        List<StatementNode> resolved,
        bool isStd = false)
    {
        string? moduleSource = null;

        // Path resolution
        string relativeDir = Path.GetDirectoryName(Path.GetFullPath(context.SourceName)) ?? Directory.GetCurrentDirectory();
        string relativePath = Path.Combine(relativeDir, $"{moduleName}.snek");
        string stdLibPath = Path.Combine(AppContext.BaseDirectory, "std", $"{moduleName}.snek");
        string cwdPath = Path.Combine(Directory.GetCurrentDirectory(), $"{moduleName}.snek");
        string devPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Snek", "std", $"{moduleName}.snek");

        if (isStd)
        {
            if (File.Exists(stdLibPath))
            {
                moduleSource = File.ReadAllText(stdLibPath);
            }
            else if (File.Exists(devPath))
            {
                moduleSource = File.ReadAllText(devPath);
            }
        }
        else
        {
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
        }

        if (moduleSource == null)
        {
            context.Diagnostics.Add(new(
                context.SourceName,
                $"Module '{moduleName}' not found",
                line, col,
                DiagnosticSeverity.Error));
            return;
        }

        // Tokenize module source code
        IEnumerable<Token> rawTokens = _lexer.Tokenize(moduleSource, context);
        rawTokens = MergeDoubleColonIdentifiers(rawTokens);

        AstNode rawModuleAst = _parser.Parse(rawTokens, context);

        if (rawModuleAst is ProgramNode rawModuleProgram)
        {
            string finalMangledModuleName = isStd ? $"std_{moduleName}" : moduleName;
            ModuleMetadata meta = new(finalMangledModuleName, rawModuleProgram);
            moduleMetadataCache[finalMangledModuleName] = meta;

            // Build Module-Internal Rename Map
            Dictionary<string, string> internalRenameMap = [];
            foreach (KeyValuePair<string, bool> kvp in meta.Declarations)
            {
                if (kvp.Value) // IsPublic == true
                {
                    internalRenameMap[kvp.Key] = $"{finalMangledModuleName}_{kvp.Key}";
                }
                else
                {
                    internalRenameMap[kvp.Key] = $"_private_{finalMangledModuleName}_{kvp.Key}";
                }
            }

            // Rename tokens within module stream
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

            // Parse mangled tokens
            AstNode mangledModuleAst = _parser.Parse(mangledModuleTokens, context);
            if (mangledModuleAst is ProgramNode mangledModuleProgram)
            {
                List<StatementNode> resolvedMangled = ResolveImportsAndBuildAliasTable(
                    mangledModuleProgram.Statements, context, importedModules, [], moduleMetadataCache);
                resolved.AddRange(resolvedMangled);

                // Add the module's own mangled statements (classes/impl blocks) into the final AST resolved stream
                resolved.AddRange(mangledModuleProgram.Statements);
            }
        }
    }

    private static void LogStage(string sourceName, string stage)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("[Snek] ");
        Console.ResetColor();
        Console.WriteLine($"{stage} '{sourceName}'...");
    }
}