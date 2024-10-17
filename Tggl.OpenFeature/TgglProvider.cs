using OpenFeature;
using OpenFeature.Model;

namespace Tggl.OpenFeature;

public class TgglProvider: FeatureProvider
{
    private readonly TgglClient _client;
    
    public TgglProvider(string? apiKey = null, TgglClient.Options? options = null)
    {
        _client = new TgglClient(apiKey, options);
    }

    public override Metadata? GetMetadata()
    {
        return new Metadata("Tggl Provider");
    }

    public override async Task<ResolutionDetails<bool>> ResolveBooleanValueAsync(string flagKey, bool defaultValue, EvaluationContext? context = null,
        CancellationToken cancellationToken = new CancellationToken())
    {
        var flags = await _client.EvalContextAsync((object?) context?.AsDictionary() ?? new {});
        return new ResolutionDetails<bool>(flagKey, flags.GetBoolean(flagKey, defaultValue));
    }

    public override async Task<ResolutionDetails<string>> ResolveStringValueAsync(string flagKey, string defaultValue, EvaluationContext? context = null,
        CancellationToken cancellationToken = new CancellationToken())
    {
        var flags = await _client.EvalContextAsync((object?) context?.AsDictionary() ?? new {});
        return new ResolutionDetails<string>(flagKey, flags.GetString(flagKey, defaultValue));
    }

    public override async Task<ResolutionDetails<int>> ResolveIntegerValueAsync(string flagKey, int defaultValue, EvaluationContext? context = null,
        CancellationToken cancellationToken = new CancellationToken())
    {
        var flags = await _client.EvalContextAsync((object?) context?.AsDictionary() ?? new {});
        return new ResolutionDetails<int>(flagKey, flags.GetInteger(flagKey, defaultValue));
    }

    public override async Task<ResolutionDetails<double>> ResolveDoubleValueAsync(string flagKey, double defaultValue, EvaluationContext? context = null,
        CancellationToken cancellationToken = new CancellationToken())
    {
        var flags = await _client.EvalContextAsync((object?) context?.AsDictionary() ?? new {});
        return new ResolutionDetails<double>(flagKey, flags.GetDouble(flagKey, defaultValue));
    }

    public override async Task<ResolutionDetails<Value>> ResolveStructureValueAsync(string flagKey, Value defaultValue, EvaluationContext? context = null,
        CancellationToken cancellationToken = new CancellationToken())
    {
        var flags = await _client.EvalContextAsync((object?) context?.AsDictionary() ?? new {});
        var value = flags.Get(flagKey, defaultValue);
        return new ResolutionDetails<Value>(flagKey, value is Value valValue ? valValue : new Value(value!));
    }
}
