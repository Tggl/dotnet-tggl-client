using System.Net.Http.Json;

namespace Tggl;

using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Collections;
using System.IO.Hashing;
using System.Text;

public class Flag
{
    public enum Operator
    {
        Empty,
        True,
        StrEqual,
        StrEqualSoft,
        StrStartsWith,
        StrEndsWith,
        StrContains,
        Percentage,
        ArrOverlap,
        RegExp,
        StrBefore,
        StrAfter,
        Eq,
        Lt,
        Gt,
        DateAfter,
        DateBefore,
        SemverEq,
        SemverGte,
        SemverLte
    }
    
    public class OperatorEnumConverter : JsonConverter<Operator>
    {
        public override Operator Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var enumValue = reader.GetString();
            return enumValue switch
            {
                "EMPTY" => Operator.Empty,
                "TRUE" => Operator.True,
                "STR_EQUAL" => Operator.StrEqual,
                "STR_EQUAL_SOFT" => Operator.StrEqualSoft,
                "STR_STARTS_WITH" => Operator.StrStartsWith,
                "STR_ENDS_WITH" => Operator.StrEndsWith,
                "STR_CONTAINS" => Operator.StrContains,
                "PERCENTAGE" => Operator.Percentage,
                "ARR_OVERLAP" => Operator.ArrOverlap,
                "REGEXP" => Operator.RegExp,
                "STR_BEFORE" => Operator.StrBefore,
                "STR_AFTER" => Operator.StrAfter,
                "EQ" => Operator.Eq,
                "LT" => Operator.Lt,
                "GT" => Operator.Gt,
                "DATE_AFTER" => Operator.DateAfter,
                "DATE_BEFORE" => Operator.DateBefore,
                "SEMVER_EQ" => Operator.SemverEq,
                "SEMVER_GTE" => Operator.SemverGte,
                "SEMVER_LTE" => Operator.SemverLte,
                _ => throw new JsonException($"Unknown enum value: {enumValue}")
            };
        }

        public override void Write(Utf8JsonWriter writer, Operator value, JsonSerializerOptions options)
        {
            var enumString = value switch
            {
                Operator.Empty => "EMPTY",
                Operator.True => "TRUE",
                Operator.StrEqual => "STR_EQUAL",
                Operator.StrEqualSoft => "STR_EQUAL_SOFT",
                Operator.StrStartsWith => "STR_STARTS_WITH",
                Operator.StrEndsWith => "STR_ENDS_WITH",
                Operator.StrContains => "STR_CONTAINS",
                Operator.Percentage => "PERCENTAGE",
                Operator.ArrOverlap => "ARR_OVERLAP",
                Operator.RegExp => "REGEXP",
                Operator.StrBefore => "STR_BEFORE",
                Operator.StrAfter => "STR_AFTER",
                Operator.Eq => "EQ",
                Operator.Lt => "LT",
                Operator.Gt => "GT",
                Operator.DateAfter => "DATE_AFTER",
                Operator.DateBefore => "DATE_BEFORE",
                Operator.SemverEq => "SEMVER_EQ",
                Operator.SemverGte => "SEMVER_GTE",
                Operator.SemverLte => "SEMVER_LTE",
                _ => throw new JsonException($"Unknown enum value: {value}")
            };

            writer.WriteStringValue(enumString);
        }
    }
    
    public class Rule
    {
        public string Key { get; set; }
        public Operator Operator { get; set; }
        public bool? Negate { get; set; }
        public double? RangeStart { get; set; }
        public double? RangeEnd { get; set; }
        public long? Seed { get; set; }
        public string[]? Values { get; set; }
        public object? Value { get; set; }
        public int[]? Version { get; set; }
        public double? Timestamp { get; set; }
        public string? Iso { get; set; }
    }
    
    public class Variation
    {
        public bool Active { get; set; }
        public object? Value { get; set; }
    }
    
    public class Condition
    {
        public List<Rule> Rules { get; set; }
        public Variation Variation { get; set; }
    }
    
    public string Slug { get; set; }
    public Variation DefaultVariation { get; set; }
    public List<Condition> Conditions { get; set; }
}

public class TgglLocalClient
{
    public class Options
    {
        public string? Url { get; set; } = null;
        public string? BaseUrl { get; set; } = null;
        public object? Reporting { get; set; } = null;
        public int? PollingInterval { get; set; } = null;
        public Dictionary<string, Flag>? InitialConfig { get; set; } = null;
    }
    
    private readonly string _url;
    private readonly string? _apiKey;
    private readonly TgglReporting? _reporting;
    private readonly HttpClient _client = new HttpClient(new HttpClientHandler
    {
        AllowAutoRedirect = false,
    });
    private readonly Timer _timer;
    private Dictionary<string, Flag> _config;
    private readonly TaskCompletionSource<bool> _firstConfigCompletionSource = new TaskCompletionSource<bool>();

    public TgglLocalClient(string? apiKey = null, Options? options = null)
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
                AppPrefix = reporting.AppPrefix ?? $"dotnet-client:1.0.0/TgglLocalClient",
                ApiKey = reporting.ApiKey ?? _apiKey,
                Url = reporting.Url,
                BaseUrl = reporting.BaseUrl ?? options?.BaseUrl,
                ReportInterval = reporting.ReportInterval,
            });
        }

        _config = options?.InitialConfig ?? new Dictionary<string, Flag>();
        _url = options?.Url ?? (options?.BaseUrl is null ? "https://api.tggl.io/config" : options.BaseUrl + "/config");
        _timer = new Timer(FetchConfig, null, TimeSpan.Zero,
            TimeSpan.FromMilliseconds(options?.PollingInterval ?? 5000));
    }

    private void FetchConfig(object? state)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, _url);

        if (_apiKey != null)
        {
            request.Headers.Add("x-tggl-api-key", _apiKey);
        }

        var response = _client.Send(request);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = response.Content.ReadFromJsonAsync<Dictionary<string, object?>>().Result;
            throw new Exception($"Invalid response from Tggl API ({response.StatusCode}): {errorBody?.GetValueOrDefault("error", "")}");
        }

        var result = response.Content.ReadFromJsonAsync<List<Flag>>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new Flag.OperatorEnumConverter() },
        }).Result;

        if (result is null)
        {
            return;
        }

        lock (_config)
        {
            _config = result.ToDictionary(flag => flag.Slug, flag => flag);
        }
        
        _firstConfigCompletionSource.TrySetResult(true);
    }

    public async Task Ready()
    {
        await _firstConfigCompletionSource.Task;
    }

    public void Dispose()
    {
        _timer.Dispose();
    }

    public Dictionary<string, Flag> GetConfig()
    {
        return _config;
    }

    public void SetConfig(Dictionary<string, Flag> config)
    {
        lock (_config)
        {
            _config = config;
        }
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

    private static bool EvalRule(Flag.Rule rule, object? value)
    {
        var ruleValue = ConvertToPrimitive(rule.Value);
        
        if (rule.Operator == Flag.Operator.Empty)
        {
            var isEmpty = value is null or "";
            return isEmpty != rule.Negate;
        }

        if (value is null)
        {
            return false;
        }

        if (rule.Operator == Flag.Operator.StrEqual)
        {
            if (value is not string strValue)
            {
                return false;
            }

            return rule.Values?.Contains(strValue) != rule.Negate;
        }

        if (rule.Operator == Flag.Operator.StrEqualSoft)
        {
            if (value is not string && value is not int && value is not long && value is not double && value is not float)
            {
                return false;
            }

            return rule.Values?.Contains(value.ToString()?.ToLower() ?? "") != rule.Negate;
        }

        if (rule.Operator == Flag.Operator.StrContains)
        {
            if (value is not string strValue)
            {
                return false;
            }

            return rule.Values?.Any(val => strValue.Contains(val)) != rule.Negate;
        }

        if (rule.Operator == Flag.Operator.StrStartsWith)
        {
            if (value is not string strValue)
            {
                return false;
            }

            return rule.Values?.Any(val => strValue.StartsWith(val)) != rule.Negate;
        }

        if (rule.Operator == Flag.Operator.StrEndsWith)
        {
            if (value is not string strValue)
            {
                return false;
            }

            return rule.Values?.Any(val => strValue.EndsWith(val)) != rule.Negate;
        }

        if (rule.Operator == Flag.Operator.StrAfter)
        {
            if (value is not string strValue || ruleValue is not string)
            {
                return false;
            }

            return string.Compare(strValue, (string) ruleValue, StringComparison.Ordinal) >= 0 != (rule.Negate ?? false);
        }

        if (rule.Operator == Flag.Operator.StrBefore)
        {
            if (value is not string strValue || ruleValue is not string)
            {
                return false;
            }

            return string.Compare(strValue, (string) ruleValue, StringComparison.Ordinal) <= 0 != (rule.Negate ?? false);
        }

        if (rule.Operator == Flag.Operator.RegExp)
        {
            if (value is not string strValue || ruleValue is not string)
            {
                return false;
            }

            return new Regex((string) ruleValue).IsMatch(strValue) != rule.Negate;
        }

        if (rule.Operator == Flag.Operator.True)
        {
            return value.Equals(!rule.Negate);
        }

        if (rule.Operator == Flag.Operator.Eq)
        {
            if (value is not double && value is not int && value is not float && value is not long)
            {
                return false;
            }
            
            return Math.Abs(Convert.ToDouble(value) - Convert.ToDouble(ruleValue)) < 0.0000000001 != rule.Negate;
        }

        if (rule.Operator == Flag.Operator.Lt)
        {
            if (value is not double && value is not int && value is not float && value is not long)
            {
                return false;
            }

            return (Convert.ToDouble(value) < Convert.ToDouble(ruleValue)) != rule.Negate;
        }

        if (rule.Operator == Flag.Operator.Gt)
        {
            if (value is not double && value is not int && value is not float && value is not long)
            {
                return false;
            }

            return (Convert.ToDouble(value) > Convert.ToDouble(ruleValue)) != rule.Negate;
        }

        if (rule.Operator == Flag.Operator.ArrOverlap)
        {
            if (value is not List<object?> arrayValue)
            {
                return false;
            }

            return arrayValue.Any(val => rule.Values?.Contains(val?.ToString()) ?? false) != rule.Negate;
        }

        if (rule.Operator == Flag.Operator.DateAfter)
        {
            if (value is string strValue)
            {
                var val = strValue.Substring(0, Math.Min("2000-01-01T23:59:59".Length, strValue.Length)) + "2000-01-01T23:59:59".Substring(Math.Min("2000-01-01T23:59:59".Length, strValue.Length));
                return string.Compare(rule.Iso, val, StringComparison.Ordinal) <= 0 != (rule.Negate ?? false);
            }

            if (value is long or double or int or float)
            {
                if (Convert.ToDouble(value) < 631152000000)
                {
                    return Convert.ToDouble(value) * 1000 >= rule.Timestamp != (rule.Negate ?? false);
                }

                return Convert.ToDouble(value) >= rule.Timestamp != (rule.Negate ?? false);
            }

            return false;
        }

        if (rule.Operator == Flag.Operator.DateBefore)
        {
            if (value is string strValue)
            {
                var val = strValue.Substring(0, Math.Min("2000-01-01T00:00:00".Length, strValue.Length)) + "2000-01-01T00:00:00".Substring(Math.Min("2000-01-01T00:00:00".Length, strValue.Length));
                return string.Compare(rule.Iso, val, StringComparison.Ordinal) >= 0 != (rule.Negate ?? false);
            }

            if (value is long or double or int or float)
            {
                if (Convert.ToDouble(value) < 631152000000)
                {
                    return Convert.ToDouble(value) * 1000 <= rule.Timestamp != (rule.Negate ?? false);
                }

                return Convert.ToDouble(value) <= rule.Timestamp != (rule.Negate ?? false);
            }

            return false;
        }

        if (rule.Operator is Flag.Operator.SemverEq)
        {
            if (value is not string strValue)
            {
                return false;
            }
        
            var semVer = strValue.Split('.').Select(int.Parse).ToArray();
        
            for (var i = 0; i < rule.Version?.Length; i++)
            {
                if (i >= semVer.Length || semVer[i] != rule.Version[i])
                {
                    return rule.Negate ?? false;
                }
            }
        
            return !(rule.Negate ?? false);
        }

        if (rule.Operator is Flag.Operator.SemverGte)
        {
            if (value is not string strValue)
            {
                return false;
            }
        
            var semVer = strValue.Split('.').Select(int.Parse).ToArray();
        
            for (var i = 0; i < rule.Version?.Length; i++)
            {
                if (i >= semVer.Length)
                {
                    return rule.Negate ?? false;
                }

                if (semVer[i] > rule.Version[i])
                {
                    return !(rule.Negate ?? false);
                }

                if (semVer[i] < rule.Version[i])
                {
                    return rule.Negate ?? false;
                }
            }
        
            return !(rule.Negate ?? false);
        }

        if (rule.Operator is Flag.Operator.SemverLte)
        {
            if (value is not string strValue)
            {
                return false;
            }
        
            var semVer = strValue.Split('.').Select(int.Parse).ToArray();
        
            for (var i = 0; i < rule.Version?.Length; i++)
            {
                if (i >= semVer.Length)
                {
                    return rule.Negate ?? false;
                }

                if (semVer[i] < rule.Version[i])
                {
                    return !(rule.Negate ?? false);
                }

                if (semVer[i] > rule.Version[i])
                {
                    return rule.Negate ?? false;
                }
            }
        
            return !(rule.Negate ?? false);
        }
        
        if (rule.Operator == Flag.Operator.Percentage)
        {
            if (value is not string && value is not int && value is not double && value is not long && value is not float)
            {
                return false;
            }

            double probability = (double) XxHash32.HashToUInt32(Encoding.UTF8.GetBytes(value.ToString() ?? ""),
                (int) rule.Seed) / 0xffffffff;
            
            if (probability == 1)
            {
                probability -= 0.0000000001;
            }
            
            return (probability >= rule.RangeStart && probability < rule.RangeEnd) != (rule.Negate ?? false);
        }

        throw new InvalidOperationException($"Unsupported operator {rule.Operator}");
    }

    private static bool EvalRules(object context, List<Flag.Rule> rules)
    {
        return rules.All(rule =>
        {
            if (context is JsonElement jsonElement && jsonElement.TryGetProperty(rule.Key, out JsonElement jsonProperty))
            {
                return EvalRule(rule, ConvertToPrimitive(jsonProperty));
            }

            if (context is IDictionary dictionary && dictionary.Contains(rule.Key))
            {
                return EvalRule(rule, ConvertToPrimitive(dictionary[rule.Key]));
            }

            var propertyInfo = context.GetType().GetProperty(rule.Key);
            if (propertyInfo != null)
            {
                return EvalRule(rule, ConvertToPrimitive(propertyInfo.GetValue(context)));
            }

            return EvalRule(rule, null);
        });
    }

    public static Flag.Variation EvalFlag(object context, Flag flag)
    {
        foreach (var condition in flag.Conditions)
        {
            if (EvalRules(context, condition.Rules))
            {
                return new Flag.Variation
                {
                    Active = condition.Variation.Active,
                    Value = condition.Variation.Active ? ConvertToPrimitive(condition.Variation.Value) : null,
                };
            }
        }

        return new Flag.Variation
        {
            Active = flag.DefaultVariation.Active,
            Value = flag.DefaultVariation.Active ? ConvertToPrimitive(flag.DefaultVariation.Value) : null,
        };
    }

    private Flag.Variation EvalFlag(object context, string slug)
    {
        lock (_config)
        {
            if (!_config.TryGetValue(slug, out var flag))
            {
                return new Flag.Variation
                {
                    Active = false,
                };
            }

            return EvalFlag(context, flag);
        }
    }

    public bool IsActive(object context, string slug)
    {
        var variation = EvalFlag(context, slug);

        _reporting?.ReportFlag(slug, new TgglReporting.FlagData
        {
            Active = variation.Active,
            Value = variation.Value,
        });
        _reporting?.ReportContext(context);

        return variation.Active;
    }

    public object? Get(object context, string slug, object? defaultValue)
    {
        var variation = EvalFlag(context, slug);
        var value = variation.Active ? variation.Value : defaultValue;

        _reporting?.ReportFlag(slug, new TgglReporting.FlagData
        {
            Active = value != null,
            Default = defaultValue,
            Value = value,
        });
        _reporting?.ReportContext(context);

        return value;
    }

    public bool GetBoolean(object context, string slug, bool defaultValue)
    {
        var variation = EvalFlag(context, slug);
        var value = variation is { Active: true, Value: bool boolValue } ? boolValue : defaultValue;

        _reporting?.ReportFlag(slug, new TgglReporting.FlagData
        {
            Active = true,
            Default = defaultValue,
            Value = value,
        });
        _reporting?.ReportContext(context);

        return value;
    }

    public string GetString(object context, string slug, string defaultValue)
    {
        var variation = EvalFlag(context, slug);
        var value = variation is { Active: true, Value: string strValue } ? strValue : defaultValue;

        _reporting?.ReportFlag(slug, new TgglReporting.FlagData
        {
            Active = true,
            Default = defaultValue,
            Value = value,
        });
        _reporting?.ReportContext(context);

        return value;
    }

    public int GetInteger(object context, string slug, int defaultValue)
    {
        var variation = EvalFlag(context, slug);
        var value = variation is { Active: true, Value: int intValue } ? intValue : defaultValue;

        _reporting?.ReportFlag(slug, new TgglReporting.FlagData
        {
            Active = true,
            Default = defaultValue,
            Value = value,
        });
        _reporting?.ReportContext(context);

        return value;
    }

    public double GetDouble(object context, string slug, double defaultValue)
    {
        var variation = EvalFlag(context, slug);
        var value = variation is { Active: true, Value: double doubleValue } ? doubleValue : defaultValue;

        _reporting?.ReportFlag(slug, new TgglReporting.FlagData
        {
            Active = true,
            Default = defaultValue,
            Value = value,
        });
        _reporting?.ReportContext(context);

        return value;
    }

    public List<object?> GetList(object context, string slug, List<object?> defaultValue)
    {
        var variation = EvalFlag(context, slug);
        var value = variation is { Active: true, Value: List<object?> listValue } ? listValue : defaultValue;

        _reporting?.ReportFlag(slug, new TgglReporting.FlagData
        {
            Active = true,
            Default = defaultValue,
            Value = value,
        });
        _reporting?.ReportContext(context);

        return value;
    }

    public Dictionary<string, object?> GetDictionary(object context, string slug, Dictionary<string, object?> defaultValue)
    {
        var variation = EvalFlag(context, slug);
        var value = variation is { Active: true, Value: Dictionary<string, object?> dictValue } ? dictValue : defaultValue;

        _reporting?.ReportFlag(slug, new TgglReporting.FlagData
        {
            Active = true,
            Default = defaultValue,
            Value = value,
        });
        _reporting?.ReportContext(context);

        return value;
    }
    
    public Dictionary<string, object?> GetAllActiveFlags(object context)
    {
        lock (_config)
        {
            return _config
                .ToDictionary(pair => pair.Key, pair => EvalFlag(context, pair.Value))
                .Where(pair => pair.Value.Active)
                .ToDictionary(pair => pair.Key, pair => pair.Value.Value);
        }
    }
}