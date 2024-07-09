using InfluxData.Net.InfluxDb.Models;
using OpenAIAPI_Rystem.Services;
using Shared;

namespace OpenAIAPI_Rystem.Functions;

public class InfluxQueryAPIFunction : FunctionBase<InfluxQueryRequest, InfluxResponse>
{
    public const string FUNCTION_NAME = "query_influx";

    public override string Name => FUNCTION_NAME;
    public override string Description => "Queries an influx database given a host, database and influxql query.  Host and database have defaults, but if host is set we must include the protocol and port (http://127.0.0.1:8086 typicaly)";
    
    private readonly IInfluxService _influx;

    public InfluxQueryAPIFunction(IInfluxService influx, IFunctionInvocationEmitter invocationEmitter)
        : base(invocationEmitter)
    {
        _influx = influx ?? throw new ArgumentNullException(nameof(influx));
    }

    protected override async Task<InfluxResponse> ExecuteFunctionAsync(InfluxQueryRequest request)
    {
        return await _influx.Query(request);
    }
}

public class InfluxWriteAPIFunction : FunctionBase<InfluxWriteRequest, InfluxResponse>
{
    public const string FUNCTION_NAME = "write_influx";

    public override string Name => FUNCTION_NAME;
    public override string Description => "Writes a point to an influx database given a host, database and point datastructure. Timestamp is mandatory and should use standard C# formatting.  Host and database have defaults, but if host is set we must include the protocol and port (http://127.0.0.1:8086 typicaly)";
    
    private readonly IInfluxService _influx;

    public InfluxWriteAPIFunction(IInfluxService influx, IFunctionInvocationEmitter invocationEmitter)
        : base(invocationEmitter)
    {
        _influx = influx ?? throw new ArgumentNullException(nameof(influx));
    }

    protected override async Task<InfluxResponse> ExecuteFunctionAsync(InfluxWriteRequest request)
    {
        return await _influx.Write(request);
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