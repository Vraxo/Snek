namespace Snek.Pipeline;

public record PipelineOptions
{
    public bool EnableLogging { get; set; } = false;
    public bool EnableOptimizations { get; set; } = false;
    public TargetPlatform Target { get; set; } = TargetPlatform.X86;
}
