namespace Tggl;

using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

public class TgglClient
{
    public class Options
    {
        public string? Url { get; set; } = null;
        public string? BaseUrl { get; set; } = null;
        public object? Reporting { get; set; } = null;
    }
    
    private readonly string _url;
    private readonly string? _apiKey;
    private readonly TgglReporting? _reporting;
    private readonly HttpClient _client = new HttpClient(new HttpClientHandler
    {
        AllowAutoRedirect = false,
    });

    public TgglClient(string? apiKey = null, Options? options = null)
    {
        _client.Timeout = TimeSpan.FromSeconds(10);
        _apiKey = apiKey;

        if (!(options?.Reporting?.Equals(false) ?? false))
        {
            var reporting = options?.Reporting is TgglReporting.Options reportingOptions
                ? reportingOptions
                : new TgglReporting.Options();

            _reporting = new TgglReporting(new TgglReporting.Options
            {
                App = reporting.App,
                AppPrefix = reporting.AppPrefix ?? $"dotnet-client:1.0.0/TgglClient",
                ApiKey = reporting.ApiKey ?? _apiKey,
                Url = reporting.Url,
                BaseUrl = reporting.BaseUrl ?? options?.BaseUrl,
                ReportInterval = reporting.ReportInterval,
            });
        }

        _url = options?.Url ?? (options?.BaseUrl is null ? "https://api.tggl.io/flags" : options.BaseUrl + "/flags");
    }

    public async Task<TgglResponse> EvalContextAsync(object evalContext)
    {
        var responses = await EvalContextsAsync(new List<object> { evalContext });
        return responses[0];
    }
    
    private static object? ConvertToPrimitive(object? obj)
    {
        if (obj is JsonElement jsonElement)
        {
            switch (jsonElement.ValueKind)
            {
                case JsonValueKind.String:
                    return jsonElement.GetString();
                case JsonValueKind.Number:
                    if (jsonElement.TryGetInt32(out var intValue))
                    {
                        return intValue;
                    }
                    return jsonElement.GetDouble();
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return jsonElement.GetBoolean();
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return null;
                case JsonValueKind.Object:
                    return jsonElement.EnumerateObject().ToDictionary(property => property.Name, property => ConvertToPrimitive(property.Value));
                case JsonValueKind.Array:
                    return jsonElement.EnumerateArray().Select(element => ConvertToPrimitive(element)).ToList();
                default:
                    throw new InvalidOperationException($"Unsupported JsonElement ValueKind '{jsonElement.ValueKind}' encountered.");
            }
        }

        return obj;
    }
    
    public async Task<List<TgglResponse>> EvalContextsAsync(List<object> evalContexts)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _url);

            if (_apiKey != null)
            {
                request.Headers.Add("x-tggl-api-key", _apiKey);
            }

            request.Content = new StringContent(JsonSerializer.Serialize(evalContexts));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var response = await _client.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadFromJsonAsync<Dictionary<string, object?>>();
                throw new Exception($"Invalid response from Tggl API ({response.StatusCode}): {errorBody?.GetValueOrDefault("error", "")}");
            }
            
            var result = await response.Content.ReadFromJsonAsync<List<Dictionary<string, object?>>>();

            if (result is null)
            {
                throw new Exception("Invalid response");
            }
            
            return result.Select(r => new TgglResponse(r.ToDictionary(
                entry => entry.Key,
                entry => ConvertToPrimitive(entry.Value)
            ), new TgglResponse.Options() { Reporting = _reporting })).ToList();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Could not eval context: {e.Message}");
            return evalContexts
                .Select(_ => new TgglResponse(default, new TgglResponse.Options() { Reporting = _reporting })).ToList();
        }
    }
}