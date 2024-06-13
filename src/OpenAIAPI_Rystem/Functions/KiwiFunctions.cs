using MDT_API;
using Shared;

namespace OpenAIAPI_Rystem.Functions;

public class FetchKiwiFileFunction : FunctionBase
{
    public override string Name => "fetch_kiwi_file";

    public override string Description => $"Fetches a kiwi configuration file and returns the contents, or an error if there was one.  Parameters include the file descriptor name (can be any of '{String.Join(',',KiwiFiles.DescriptorLookup.Keys)}') as well as the ipv4 address (not protocol/port).";

    public override Type Input => typeof(FetchKiwiFileParameters);

    readonly IKiwiService kiwiService;

    public FetchKiwiFileFunction(IKiwiService kiwiService, IFunctionInvocationEmitter emitter) 
        : base(emitter)
    {
        this.kiwiService = kiwiService;
    }

    protected override async Task<object> ExecuteFunctionAsync(object request)
    {
        try
        {
            FetchKiwiFileParameters parameters = (FetchKiwiFileParameters)request;

            var file = KiwiFiles.DescriptorLookup[parameters.Name];
            var content = await kiwiService.FetchFile(parameters.IPAddress, file);

            return new FetchKiwiFileResult() { FileContent = content };
        }
        catch (Exception ex)
        {
            return new FetchKiwiFileResult() { Error = ex.ToString() };
        }
    }
}

public class GetKiwiSensorValuesFunction : FunctionBase
{
    public override string Name => "get_current_sensor_values";

    public override string Description => $"Queries the current values of all sensor values from kiwi, with an optional filter.  If the filter is null, all sensor values are retrieved.  Do not try to use a filter unless you know the EXACT sensor key - either from a previous sensor value request or after parsing a config file that contains it.  The key is not the same as a name and must be exact so it is not useful to guess.  If we get an entry with NO value, it is not a supported sensor.  Kiwi will return all the sensors provided in a filter even if they don't exist...  Also if we get FLT_MAX back (value not string) it means the sensor is NOT CONNECTED meaning it does not have any value but is in fact configured by kiwi.";

    public override Type Input => typeof(GetKiwiSensorValuesParameters);

    readonly IKiwiService kiwiService;

    public GetKiwiSensorValuesFunction(IKiwiService kiwiService, IFunctionInvocationEmitter emitter)
        : base(emitter)
    {
        this.kiwiService = kiwiService;
    }

    protected override async Task<object> ExecuteFunctionAsync(object request)
    {
        try
        {
            GetKiwiSensorValuesParameters parameters = (GetKiwiSensorValuesParameters)request;

            var content = await kiwiService.GetSensorValues(parameters.IPAddress, parameters.Filter);

            return new GetKiwiSensorValuesResult() { Content = content };
        }
        catch (Exception ex)
        {
            return new GetKiwiSensorValuesResult() { Error = ex.ToString() };
        }
    }
}

public class GetKiwiSensorConfigsFunction : FunctionBase
{
    public override string Name => "get_current_sensor_configs";

    public override string Description => $"Queries the current configuration of all sensors from kiwi.  Include things like signal range, sensor range, etc";

    public override Type Input => typeof(GetKiwiSensorConfigsParameters);

    readonly IKiwiService kiwiService;

    public GetKiwiSensorConfigsFunction(IKiwiService kiwiService, IFunctionInvocationEmitter emitter)
        : base(emitter)
    {
        this.kiwiService = kiwiService;
    }

    protected override async Task<object> ExecuteFunctionAsync(object request)
    {
        try
        {
            GetKiwiSensorConfigsParameters parameters = (GetKiwiSensorConfigsParameters)request;

            var content = await kiwiService.GetSensorConfigs(parameters.IPAddress);

            return new GetKiwiSensorConfigsResult() { Content = content };
        }
        catch (Exception ex)
        {
            return new GetKiwiSensorConfigsResult() { Error = ex.ToString() };
        }
    }
}

public class GetKiwiAlarmConfigsFunction : FunctionBase
{
    public override string Name => "get_alarm_configs";

    public override string Description => $"Queries the current configuration of all alarms and alarm constraints.  Include things like setpoints, hold time (how long it must be active to trip), reset time (the opposite - how long a tripped constraint must be inactive to clear), whether it's enabled, the associated sensor etc.";

    public override Type Input => typeof(GetKiwiAlarmConfigsParameters);

    readonly IKiwiService kiwiService;

    public GetKiwiAlarmConfigsFunction(IKiwiService kiwiService, IFunctionInvocationEmitter emitter)
        : base(emitter)
    {
        this.kiwiService = kiwiService;
    }

    protected override async Task<object> ExecuteFunctionAsync(object request)
    {
        try
        {
            GetKiwiAlarmConfigsParameters parameters = (GetKiwiAlarmConfigsParameters)request;

            var content = await kiwiService.GetAlarmConfigs(parameters.IPAddress);

            return new GetKiwiAlarmConfigsResult() { Content = content };
        }
        catch (Exception ex)
        {
            return new GetKiwiAlarmConfigsResult() { Error = ex.ToString() };
        }
    }
}

public class GetKiwiAlarmsFunction : FunctionBase
{
    public override string Name => "get_active_alarms";

    public override string Description => $"Queries the current active alarms (both kiwi and j1939 faults)";

    public override Type Input => typeof(GetKiwiAlarmsParameters);

    readonly IKiwiService kiwiService;

    public GetKiwiAlarmsFunction(IKiwiService kiwiService, IFunctionInvocationEmitter emitter)
        : base(emitter)
    {
        this.kiwiService = kiwiService;
    }

    protected override async Task<object> ExecuteFunctionAsync(object request)
    {
        try
        {
            GetKiwiAlarmsParameters parameters = (GetKiwiAlarmsParameters)request;

            var content = await kiwiService.GetAlarms(parameters.IPAddress);

            return new GetKiwiAlarmsResult() { Content = content };
        }
        catch (Exception ex)
        {
            return new GetKiwiAlarmsResult() { Error = ex.ToString() };
        }
    }
}

public class GetKiwiAlarmConfigsParameters
{
    public string IPAddress { get; set; }
}

public class GetKiwiSensorConfigsParameters
{
    public string IPAddress { get; set; }
}

public class GetKiwiSensorConfigsResult
{
    public string Error { get; set; }
    public string Content { get; set; }
}

public class GetKiwiAlarmConfigsResult
{
    public string Error { get; set; }
    public string Content { get; set; }
}

public class GetKiwiAlarmsParameters
{
    public string IPAddress { get; set; }
}

public class GetKiwiAlarmsResult
{
    public string Error { get; set; }
    public string Content { get; set; }
}

public class GetKiwiSensorValuesParameters
{
    public string IPAddress { get; set; }
    public string[] Filter { get; set; }
}

public class GetKiwiSensorValuesResult
{
    public string Error { get; set; }
    public string Content { get; set; }
}

public class FetchKiwiFileParameters
{
    public string Name { get; set; }
    public string IPAddress { get; set; }
}

public class FetchKiwiFileResult
{
    public string Error { get; set; }
    public string FileContent { get; set; }
}
