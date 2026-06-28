using FluentAssertions;
using Snek.Core.Analysis;
using Snek.Core.Generation;
using Snek.Core.Lexing;
using Snek.Core.Parsing;
using Snek.Core.Pipeline;

namespace Snek.Tests.Pipeline;

public class CompilerPipelineTests
{
    [Fact]
    public void Compile_ValidProgram_ReturnsSuccess()
    {
        CompilerPipeline pipeline = CreateDefaultPipeline();
        string source = "pass;\n";

        CompilationResult result = pipeline.Compile(source, "test.snek");

        result.Success.Should().BeTrue();
        result.Output.Should().NotBeNull();
        result.Diagnostics.Should().NotContain(d => d.IsError);
    }

    [Fact]
    public void Compile_LexicalError_ReturnsFailure()
    {
        CompilerPipeline pipeline = CreateDefaultPipeline();
        string source = "\"unterminated";

        CompilationResult result = pipeline.Compile(source, "test.snek");

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.IsError && d.Message.Contains("Unterminated"));
    }

    [Fact]
    public void Compile_SyntaxError_ReturnsFailure()
    {
        CompilerPipeline pipeline = CreateDefaultPipeline();
        string source = "fn invalid(:";

        CompilationResult result = pipeline.Compile(source, "test.snek");

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.IsError);
    }

    [Fact]
    public void Compile_SemanticError_ReturnsFailure()
    {
        CompilerPipeline pipeline = CreateDefaultPipeline();
        string source = """
            fn foo() -> int {
              return "wrong";
            }
            """;

        CompilationResult result = pipeline.Compile(source, "test.snek");

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.Message.Contains("Return type mismatch"));
    }

    [Fact]
    public void Compile_WithVerboseOption_LogsStages()
    {
        PipelineOptions options = new() { EnableLogging = true };
        CompilerPipeline pipeline = CreateDefaultPipeline(options);
        string source = "pass;\n";

        CompilationResult result = pipeline.Compile(source, "test.snek");

        result.Success.Should().BeTrue();
        result.Output.Should().NotBeNull();
    }

    [Fact]
    public void Compile_AssemblyOutput_ContainsExpectedSections()
    {
        CompilerPipeline pipeline = CreateDefaultPipeline();
        string source = "print(\"hello\");\n";

        CompilationResult result = pipeline.Compile(source, "test.snek");

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("section '.data'");
        result.Output.Should().Contain("section '.text'");
        result.Output.Should().Contain("section '.idata'");
    }

    private static CompilerPipeline CreateDefaultPipeline(PipelineOptions? options = null)
    {
        return new(
            new Lexer(),
            new Parser(),
            new SemanticAnalyzer(),
            new CodeGenerator(),
            options);
    }
}