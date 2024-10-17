namespace Tggl;

using System.Threading;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Net.Http.Headers;

public class TgglReporting
{
    public class Options
    {
        public string? App { get; set; }
        public string? AppPrefix { get; set; }
        public string? ApiKey { get; set; }
        public string? Url { get; set; }
        public string? BaseUrl { get; set; }
        public int? ReportInterval { get; set; }
    }
    
    public class FlagData
    {
        public bool Active { get; set; }
        public object? Value { get; set; }
        public object? Default { get; set; }
        public int? Count { get; set; }
    }
    
    private readonly string? _app;
    private readonly string? _appPrefix;
    private readonly string? _apiKey;
    private readonly string _url;
    private readonly HttpClient _client = new HttpClient(new HttpClientHandler
    {
        AllowAutoRedirect = false,
    });

    private readonly Dictionary<string, Dictionary<string, Dictionary<string, FlagData>>> _flagsToReport =
        new();

    private readonly Dictionary<string, (int, int)> _receivedPropertiesToReport = new();
    private Dictionary<string, Dictionary<string, string?>> _receivedValuesToReport = new();
    private readonly Timer _timer;

    public TgglReporting(Options? options = null)
    {
        _client.Timeout = TimeSpan.FromSeconds(20);
        _app = options?.App;
        _appPrefix = options?.AppPrefix;
        _apiKey = options?.ApiKey;
        _url = options?.Url ?? (options?.BaseUrl is null ? "https://api.tggl.io/report" : options.BaseUrl + "/report");
        _timer = new Timer(SendReport, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(options?.ReportInterval ?? 5000));
    }
    
    ~TgglReporting()
    {
        SendReport(null);
    }

    public void Dispose()
    {
        _timer.Dispose();
    }

    private void ApiCall(Dictionary<string, object> body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _url);

        if (_apiKey != null)
        {
            request.Headers.Add("x-tggl-api-key", _apiKey);
        }

        request.Content = new StringContent(JsonSerializer.Serialize(body));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        _client.Send(request);
    }

    private void SendReport(object? state)
    {
        try
        {
            var payload = new Dictionary<string, object>();

            lock (_flagsToReport)
            {
                if (_flagsToReport.Any())
                {
                    payload["clients"] = _flagsToReport.Select(client => new
                    {
                        id = client.Key,
                        flags = client.Value.ToDictionary(
                            flag => flag.Key,
                            flag => flag.Value.Values.Select(data => new Dictionary<string, object?>
                            {
                                { "active", data.Active },
                                { "value", data.Value },
                                { "default", data.Default },
                                { "count", data.Count }
                            }).ToList())
                    }).ToList();
                    _flagsToReport.Clear();
                }
            }

            lock (_receivedPropertiesToReport)
            {
                if (_receivedPropertiesToReport.Any())
                {
                    payload["receivedProperties"] = _receivedPropertiesToReport.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new List<int> { kvp.Value.Item1, kvp.Value.Item2 }
                    );
                    ;
                    _receivedPropertiesToReport.Clear();
                }
            }

            lock (_receivedValuesToReport)
            {
                if (_receivedValuesToReport.Any())
                {
                    var data = _receivedValuesToReport
                        .SelectMany(kv => kv.Value.Select(value =>
                        {
                            var label = value.Value;
                            if (label != null)
                            {
                                return new[] { kv.Key, value.Key, label };
                            }
                            else
                            {
                                return new[] { kv.Key, value.Key };
                            }
                        }))
                        .ToList();

                    var pageSize = 2000;

                    payload["receivedValues"] = data
                        .Take(pageSize)
                        .Aggregate(new Dictionary<string, List<string[]>>(), (acc, cur) =>
                        {
                            if (!acc.ContainsKey(cur[0]))
                            {
                                acc[cur[0]] = new List<string[]>();
                            }

                            acc[cur[0]].Add(cur.Skip(1).Select(v => v.Length > 240 ? v.Substring(0, 240) : v)
                                .ToArray());
                            return acc;
                        });

                    for (var i = pageSize; i < data.Count; i += pageSize)
                    {
                        ApiCall(new Dictionary<string, object>
                            {
                                {
                                    "receivedValues", data
                                        .Skip(i)
                                        .Take(pageSize)
                                        .Aggregate(new Dictionary<string, List<string[]>>(), (acc, cur) =>
                                        {
                                            if (!acc.ContainsKey(cur[0]))
                                            {
                                                acc[cur[0]] = new List<string[]>();
                                            }

                                            acc[cur[0]].Add(cur.Skip(1)
                                                .Select(v => v.Length > 240 ? v.Substring(0, 240) : v)
                                                .ToArray());
                                            return acc;
                                        })
                                }
                            }
                        );
                    }

                    _receivedValuesToReport.Clear();
                }
            }

            if (payload.Any())
            {
                ApiCall(payload);
            }
        }
        catch (Exception)
        {
            // Do nothing
        }
    }

    public void ReportFlag(string slug, FlagData data)
    {
        try
        {
            var clientId = $"{_appPrefix ?? ""}{(_app != null && _appPrefix != null ? "/" : "")}{_app ?? ""}";
            IncrementFlag(data, clientId, slug);
        }
        catch (Exception)
        {
            // Do nothing
        }
    }

    private static string ConstantCase(string str)
    {
        return Regex.Replace(Regex.Replace(str, "([a-z])([A-Z])", "$1_$2"), @"[\W_]+", "_").ToUpper();
    }

    public void ReportContext(object context)
    {
        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            lock (_receivedPropertiesToReport)
            {
                lock (_receivedValuesToReport)
                {
                    foreach (var property in context.GetType().GetProperties())
                    {
                        if (_receivedPropertiesToReport.ContainsKey(property.Name))
                        {
                            _receivedPropertiesToReport[property.Name] = (
                                _receivedPropertiesToReport[property.Name].Item1, (int)now);
                        }
                        else
                        {
                            _receivedPropertiesToReport[property.Name] = ((int)now, (int)now);
                        }

                        var value = property.GetValue(context);

                        if (value is string stringValue && !string.IsNullOrEmpty(stringValue))
                        {
                            var constantCaseKey = Regex.Replace(ConstantCase(property.Name), "_I_D$", "_ID");
                            var labelKeyTarget = constantCaseKey.EndsWith("_ID")
                                ? Regex.Replace(constantCaseKey, "_ID$", "_NAME")
                                : null;
                            var labelProperty = labelKeyTarget != null
                                ? context.GetType()
                                    .GetProperties()
                                    .FirstOrDefault(k => ConstantCase(k.Name) == labelKeyTarget)
                                : null;


                            if (!_receivedValuesToReport.ContainsKey(property.Name))
                            {
                                _receivedValuesToReport[property.Name] = new Dictionary<string, string?>();
                            }

                            _receivedValuesToReport[property.Name][stringValue] =
                                labelProperty?.GetValue(context) is string stringLabel
                                    ? stringLabel
                                    : null;
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            // Do nothing
        }
    }

    private void IncrementFlag(FlagData data, string clientId, string slug)
    {
        lock (_flagsToReport)
        {
            if (!_flagsToReport.ContainsKey(clientId))
            {
                _flagsToReport[clientId] = new Dictionary<string, Dictionary<string, FlagData>>();
            }

            var key =
                $"{(data.Active ? "1" : "0")}{data.Value?.ToString() ?? "null"}{data.Default?.ToString() ?? "null"}";

            if (!_flagsToReport[clientId].ContainsKey(slug))
            {
                _flagsToReport[clientId][slug] = new Dictionary<string, FlagData>();
            }

            if (!_flagsToReport[clientId][slug].ContainsKey(key))
            {
                _flagsToReport[clientId][slug][key] = new FlagData
                {
                    Active = data.Active,
                    Value = data.Value,
                    Default = data.Default,
                    Count = 0
                };
            }

            _flagsToReport[clientId][slug][key].Count += data.Count ?? 1;
        }
    }
}