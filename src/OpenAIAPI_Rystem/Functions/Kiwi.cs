using MDT_API;
using Rystem.OpenAi.Chat;
using Shared;

namespace OpenAIAPI_Rystem.Functions
{
    public class GetKiwiFileDescriptionsFunction : IOpenAiChatFunction
    {
        public string Name => "get_kiwi_file_descriptions";

        public string Description => "Fetches some json that describes all the different files available on kiwi.  The key is the dsecriptor to use if you want to fetch a file, the value is an object with some descriptive info in it :)";

        public Type Input => typeof(KiwiGetFileInfoFunctionParameters);

        readonly IKiwiService kiwiService = new KiwiFileService();

        public Task<object> WrapAsync(string message)
        {
            return Task.FromResult<object>(kiwiService.GetInfoForAllKiwiFiles());
        }
    }

    public class KiwiGetFileInfoFunctionParameters
    {
    }

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

        public override string Description => $"Queries the current values of all sensor values from kiwi, with an optional filter.  If the filter is null, all sensor values are retrieved.  Do not try to use a filter unless you know the EXACT sensor key - either from a previous sensor value request or after parsing config file parse.  The key is not the same as a name and must be exact so it is not useful to guess.";

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
                return new FetchKiwiFileResult() { Error = ex.ToString() };
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
}
