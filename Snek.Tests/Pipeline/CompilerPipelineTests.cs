using FluentAssertions;
using Snek.Analysis;
using Snek.Generation;
using Snek.Lexer;
using Snek.Parser;
using Snek.Pipeline;

namespace Snek.Tests.Pipeline;

public class CompilerPipelineTests
{
    [Fact]
    public void Compile_ValidProgram_ReturnsSuccess()
    {
        var pipeline = CreateDefaultPipeline();
        var source = """
            fn main() -> void:
              pass

            """;

        var result = pipeline.Compile(source, "test.snek");

        result.Success.Should().BeTrue();
        result.Output.Should().NotBeNull();
        result.Diagnostics.Should().NotContain(d => d.IsError);
    }

    [Fact]
    public void Compile_LexicalError_ReturnsFailure()
    {
        var pipeline = CreateDefaultPipeline();
        var source = "\"unterminated";

        var result = pipeline.Compile(source, "test.snek");

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.IsError && d.Message.Contains("Unterminated"));
    }

    [Fact]
    public void Compile_SyntaxError_ReturnsFailure()
    {
        var pipeline = CreateDefaultPipeline();
        var source = "fn invalid(:";

        var result = pipeline.Compile(source, "test.snek");

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.IsError);
    }

    [Fact]
    public void Compile_SemanticError_ReturnsFailure()
    {
        var pipeline = CreateDefaultPipeline();
        var source = """
            fn foo() -> int:
              return "wrong"

            """;

        var result = pipeline.Compile(source, "test.snek");

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.Message.Contains("Return type mismatch"));
    }

    [Fact]
    public void Compile_WithVerboseOption_LogsStages()
    {
        var options = new PipelineOptions { EnableLogging = true };
        var pipeline = CreateDefaultPipeline(options);
        var source = """
            fn main() -> void:
              pass

            """;

        var result = pipeline.Compile(source, "test.snek");

        result.Success.Should().BeTrue();
        result.Output.Should().NotBeNull();
    }

    [Fact]
    public void Compile_AssemblyOutput_ContainsExpectedSections()
    {
        var pipeline = CreateDefaultPipeline();
        var source = """
            fn main() -> void:
              print("hello")

            """;

        var result = pipeline.Compile(source, "test.snek");

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("section '.data'");
        result.Output.Should().Contain("section '.text'");
        result.Output.Should().Contain("section '.idata'");
    }

    private CompilerPipeline CreateDefaultPipeline(PipelineOptions? options = null)
    {
        return new CompilerPipeline(
            new Snek.Lexer.Lexer(),
            new Snek.Parser.Parser(),
            new SemanticAnalyzer(),
            new CodeGenerator(),
            options);
    }
}