using Snek.Core.Analysis;
using Snek.Core.Ast;
using Snek.Core.Diagnoistics;
using Snek.Core.Generation;
using Snek.Core.Lexing;
using Snek.Core.Parsing;

namespace Snek.Core.Pipeline;

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
                List<StatementNode> resolvedStatements = ResolveImports(program.Statements, context, importedModules);
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

    private List<StatementNode> ResolveImports(IReadOnlyList<StatementNode> statements, CompilationContext context, HashSet<string> importedModules)
    {
        List<StatementNode> resolved = [];
        foreach (StatementNode statement in statements)
        {
            if (statement is ImportStatementNode importNode)
            {
                string moduleName = importNode.ModuleName;
                if (importedModules.Contains(moduleName))
                {
                    continue; // Prevent cycle/duplicate compilation
                }
                importedModules.Add(moduleName);

                string? moduleSource = null;
                if (moduleName == "list")
                {
                    moduleSource = """
                        extern fn malloc(size: i32) -> Any;
                        extern fn realloc(ptr: Any, size: i32) -> Any;

                        class List {
                            data: Any;
                            length: i32;
                            capacity: i32;
                        }

                        impl List {
                            fn new() -> List {
                                self: List = List(0, 0, 0);
                                self.data = malloc(16);
                                self.length = 0;
                                self.capacity = 4;
                                return self;
                            }

                            fn append(self, val: Any) {
                                if self.length == self.capacity {
                                    self.capacity = self.capacity * 2;
                                    self.data = realloc(self.data, self.capacity * 4);
                                }
                                self.data[self.length] = val;
                                self.length = self.length + 1;
                            }

                            fn get(self, idx: i32) -> Any {
                                return self.data[idx];
                            }
                        }
                        """;
                }
                else
                {
                    string fileName = $"{moduleName}.snek";
                    if (File.Exists(fileName))
                    {
                        moduleSource = File.ReadAllText(fileName);
                    }
                }

                if (moduleSource == null)
                {
                    context.Diagnostics.Add(new(
                        context.SourceName,
                        $"Module '{moduleName}' not found",
                        -1, -1,
                        DiagnosticSeverity.Error));
                    continue;
                }

                Lexer lexer = new();
                Parser parser = new();
                IEnumerable<Token> tokens = lexer.Tokenize(moduleSource, context);
                AstNode importedAst = parser.Parse(tokens, context);

                if (importedAst is ProgramNode importedProgram)
                {
                    List<StatementNode> resolvedImported = ResolveImports(importedProgram.Statements, context, importedModules);
                    resolved.AddRange(resolvedImported);
                }
            }
            else
            {
                resolved.Add(statement);
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