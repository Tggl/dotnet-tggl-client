namespace Tggl.Client;

public class TgglResponse
{
    public class Options
    {
        public TgglReporting? Reporting { get; set; }
    }
    
    private TgglReporting? _reporting;
    private readonly Dictionary<string, object?> _flags;

    public TgglResponse(Dictionary<string, object?>? flags = null, Options? options = null)
    {
        this._flags = flags ?? new Dictionary<string, object?>();
        this._reporting = options?.Reporting;
    }

    public void DisableReporting()
    {
        _reporting?.Dispose();
        _reporting = null;
    }

    public bool IsActive(string slug)
    {
        var active = _flags.ContainsKey(slug);

        _reporting?.ReportFlag(slug, new TgglReporting.FlagData
        {
            Active = active,
            Value = _flags.GetValueOrDefault(slug)
        });

        return active;
    }

    public object? Get(string slug, object? defaultValue)
    {
        var value = _flags.GetValueOrDefault(slug, defaultValue);

        _reporting?.ReportFlag(slug, new TgglReporting.FlagData
        {
            Active = value != null,
            Default = defaultValue,
            Value = value
        });

        return value;
    }

    public bool GetBoolean(string slug, bool defaultValue)
    {
        var value = _flags.GetValueOrDefault(slug, null) is bool boolValue ? boolValue : defaultValue;

        _reporting?.ReportFlag(slug, new TgglReporting.FlagData
        {
            Active = true,
            Default = defaultValue,
            Value = value
        });

        return value;
    }

    public string GetString(string slug, string defaultValue)
    {
        var value = _flags.GetValueOrDefault(slug, null) is string strValue ? strValue : defaultValue;

        _reporting?.ReportFlag(slug, new TgglReporting.FlagData
        {
            Active = true,
            Default = defaultValue,
            Value = value
        });

        return value;
    }

    public int GetInteger(string slug, int defaultValue)
    {
        var value = _flags.GetValueOrDefault(slug, null) is int intValue ? intValue : defaultValue;

        _reporting?.ReportFlag(slug, new TgglReporting.FlagData
        {
            Active = true,
            Default = defaultValue,
            Value = value
        });

        return value;
    }

    public double GetDouble(string slug, double defaultValue)
    {
        var value = _flags.GetValueOrDefault(slug, null) is double doubleValue ? doubleValue : defaultValue;

        _reporting?.ReportFlag(slug, new TgglReporting.FlagData
        {
            Active = true,
            Default = defaultValue,
            Value = value
        });

        return value;
    }

    public List<object?> GetList(string slug, List<object?> defaultValue)
    {
        var value = _flags.GetValueOrDefault(slug, null) is List<object?> listValue ? listValue : defaultValue;

        _reporting?.ReportFlag(slug, new TgglReporting.FlagData
        {
            Active = true,
            Default = defaultValue,
            Value = value
        });

        return value;
    }

    public Dictionary<string, object?> GetDictionary(string slug, Dictionary<string, object?> defaultValue)
    {
        var value = _flags.GetValueOrDefault(slug, null) is Dictionary<string, object?> dictValue ? dictValue : defaultValue;

        _reporting?.ReportFlag(slug, new TgglReporting.FlagData
        {
            Active = true,
            Default = defaultValue,
            Value = value
        });

        return value;
    }

    public Dictionary<string, object?> GetAllActiveFlags()
    {
        return _flags;
    }
}