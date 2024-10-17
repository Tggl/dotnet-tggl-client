using OpenFeature;
using OpenFeature.Model;

namespace Tggl.OpenFeature;

public class TgglLocalProvider: FeatureProvider
{
    private readonly TgglLocalClient _client;
    
    public TgglLocalProvider(string? apiKey = null, TgglLocalClient.Options? options = null)
    {
        _client = new TgglLocalClient(apiKey, options);
    }

    public override Metadata? GetMetadata()
    {
        return new Metadata("Tggl Provider");
    }

    public override async Task InitializeAsync(EvaluationContext context, CancellationToken cancellationToken = new CancellationToken())
    {
        await _client.Ready();
    }

    public override async Task ShutdownAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        _client.Dispose();
    }

    public override async Task<ResolutionDetails<bool>> ResolveBooleanValueAsync(string flagKey, bool defaultValue, EvaluationContext? context = null,
        CancellationToken cancellationToken = new CancellationToken())
    {
        return new ResolutionDetails<bool>(flagKey, _client.GetBoolean((object?) context?.AsDictionary().ToDictionary(kv => kv.Key, kv => kv.Value.AsObject) ?? new {}, flagKey, defaultValue));
    }

    public override async Task<ResolutionDetails<string>> ResolveStringValueAsync(string flagKey, string defaultValue, EvaluationContext? context = null,
        CancellationToken cancellationToken = new CancellationToken())
    {
        return new ResolutionDetails<string>(flagKey, _client.GetString((object?) context?.AsDictionary().ToDictionary(kv => kv.Key, kv => kv.Value.AsObject) ?? new {}, flagKey, defaultValue));
    }

    public override async Task<ResolutionDetails<int>> ResolveIntegerValueAsync(string flagKey, int defaultValue, EvaluationContext? context = null,
        CancellationToken cancellationToken = new CancellationToken())
    {
        return new ResolutionDetails<int>(flagKey, _client.GetInteger((object?) context?.AsDictionary().ToDictionary(kv => kv.Key, kv => kv.Value.AsObject) ?? new {}, flagKey, defaultValue));
    }

    public override async Task<ResolutionDetails<double>> ResolveDoubleValueAsync(string flagKey, double defaultValue, EvaluationContext? context = null,
        CancellationToken cancellationToken = new CancellationToken())
    {
        return new ResolutionDetails<double>(flagKey, _client.GetDouble((object?) context?.AsDictionary().ToDictionary(kv => kv.Key, kv => kv.Value.AsObject) ?? new {}, flagKey, defaultValue));
    }

    public override async Task<ResolutionDetails<Value>> ResolveStructureValueAsync(string flagKey, Value defaultValue, EvaluationContext? context = null,
        CancellationToken cancellationToken = new CancellationToken())
    {
        var value = _client.Get((object?)context?.AsDictionary() ?? new { }, flagKey, defaultValue);
        return new ResolutionDetails<Value>(flagKey, value is Value valValue ? valValue : new Value(value!));
    }
}
