using InfluxData.Net.InfluxDb;
using Newtonsoft.Json;
using OpenAIAPI_Rystem.Functions;

namespace OpenAIAPI_Rystem.Services;

public interface IInfluxService
{
    Task<InfluxResponse> Query(InfluxQueryRequest request);
    Task<InfluxResponse> Write(InfluxWriteRequest request);
}

public class InfluxService : IInfluxService
{
    public async Task<InfluxResponse> Query(InfluxQueryRequest request)
    {
        try
        {
            var client = new InfluxDbClient(request.Host, "", "", InfluxData.Net.Common.Enums.InfluxDbVersion.Latest);

            var response = await client.RequestClient.QueryAsync(request.Query, HttpMethod.Get, request.Database);

            return new InfluxResponse() { Result = JsonConvert.SerializeObject(response) };
        }
        catch (Exception ex)
        {
            return new InfluxResponse() { Error = ex.Message };
        }
    }

    public async Task<InfluxResponse> Write(InfluxWriteRequest request)
    {
        try
        {
            var client = new InfluxDbClient(request.Host, "", "", InfluxData.Net.Common.Enums.InfluxDbVersion.Latest);
            var response = await client.Client.WriteAsync(request.Point.ConvertToInfluxDataPoint(), request.Database);

            return new InfluxResponse() { Result = JsonConvert.SerializeObject(response) };
        }
        catch (Exception ex)
        {
            return new InfluxResponse() { Error = ex.Message };
        }
    }
}