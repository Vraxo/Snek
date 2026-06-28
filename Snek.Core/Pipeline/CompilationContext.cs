using Snek.Core.Diagnoistics;

namespace Snek.Core.Pipeline;

public class CompilationContext
{
    public string SourceName { get; }
    public PipelineOptions Options { get; }
    public List<Diagnostic> Diagnostics { get; } = [];
    public Dictionary<string, object?> StageData { get; } = [];

    public CompilationContext(string sourceName, PipelineOptions options)
    {
        SourceName = sourceName;
        Options = options;
    }

    public T? GetStageData<T>(string key) where T : class
    {
        return StageData.TryGetValue(key, out object? value)
            ? value as T
            : null;
    }

    public void SetStageData<T>(string key, T value) where T : class
    {
        StageData[key] = value;
    }
}