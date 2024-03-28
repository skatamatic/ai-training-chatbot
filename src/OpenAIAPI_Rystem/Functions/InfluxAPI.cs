using InfluxData.Net.InfluxDb.Models;
using ServiceInterface;

namespace OpenAIAPI_Rystem.Functions;

public class InfluxQueryAPIFunction : FunctionBase
{
    public const string FUNCTION_NAME = "query_influx";

    public override string Name => FUNCTION_NAME;
    public override string Description => "Queries an influx database given a host, database and influxql query.  Host and database have defaults, but if host is set we must include the protocol and port (http://127.0.0.1:8086 typicaly)";
    public override Type Input => typeof(InfluxQueryRequest);

    private readonly IInfluxService _influx;

    public InfluxQueryAPIFunction(IInfluxService influx, IFunctionInvocationEmitter invocationEmitter)
        : base(invocationEmitter)
    {
        _influx = influx ?? throw new ArgumentNullException(nameof(influx));
    }

    protected override async Task<object> ExecuteFunctionAsync(object request)
    {
        if (request is InfluxQueryRequest influxRequest)
        {
            return await _influx.Query(influxRequest);
        }

        throw new ArgumentException("Invalid request type", nameof(request));
    }
}

public class InfluxWriteAPIFunction : FunctionBase
{
    public const string FUNCTION_NAME = "write_influx";

    public override string Name => FUNCTION_NAME;
    public override string Description => "Writes a point to an influx database given a host, database and point datastructure. Timestamp is mandatory and should use standard C# formatting.  Host and database have defaults, but if host is set we must include the protocol and port (http://127.0.0.1:8086 typicaly)";
    public override Type Input => typeof(InfluxWriteRequest);

    private readonly IInfluxService _influx;

    public InfluxWriteAPIFunction(IInfluxService influx, IFunctionInvocationEmitter invocationEmitter)
        : base(invocationEmitter)
    {
        _influx = influx ?? throw new ArgumentNullException(nameof(influx));
    }

    protected override async Task<object> ExecuteFunctionAsync(object request)
    {
        if (request is InfluxWriteRequest influxRequest)
        {
            return await _influx.Write(influxRequest);
        }

        throw new ArgumentException("Invalid request type", nameof(request));
    }
}

public class InfluxQueryRequest
{
    public string Database { get; set; } = "mdtmpc";
    public string Host { get; set; } = "http://127.0.0.1:8086";
    public string Query { get; set; }
}

public class InfluxWriteRequest
{
    public string Database { get; set; } = "mdtmpc";
    public string Host { get; set; } = "http://127.0.0.1:8086";
    public InfluxPoint Point { get; set; }
}

public class InfluxKvp
{
    public string Key { get; set; }
    public object Value { get; set; }

    public InfluxKvp(string key, object value)
    {
        Key = key;
        Value = value;
    }
}

public class InfluxPoint
{
    public string Name { get; set; }
    public InfluxKvp[] Tags { get; set; }
    public InfluxKvp[] Fields { get; set; }
    public string Timestamp { get; set; }

    public InfluxPoint()
    {
        Tags = new InfluxKvp[0];
        Fields = new InfluxKvp[0];
    }

    public Point ConvertToInfluxDataPoint()
    {
        var originalPoint = new Point
        {
            Name = this.Name,
            Timestamp = DateTime.Parse(this.Timestamp),
            Tags = Tags.ToDictionary(tag => tag.Key, tag => tag.Value),
            Fields = Fields.ToDictionary(field => field.Key, field => field.Value)
        };

        return originalPoint;
    }
}

public class InfluxResponse
{
    public string Result { get; set; }
    public string Error { get; set; }
}