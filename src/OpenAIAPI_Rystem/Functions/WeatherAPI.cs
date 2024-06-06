using Newtonsoft.Json;
using Shared;

namespace OpenAIAPI_Rystem.Functions;

public class WeatherAPIFunction : FunctionBase
{
    public const string FUNCTION_NAME = "get_current_weather";

    public override string Name => FUNCTION_NAME;
    public override string Description => "Gets the weather in a given location";
    public override Type Input => typeof(WeatherRequest);

    private readonly IWeatherService _weatherService;

    public WeatherAPIFunction(IWeatherService weatherService, IFunctionInvocationEmitter invocationEmitter)
        : base(invocationEmitter)
    {
        _weatherService = weatherService ?? throw new ArgumentNullException(nameof(weatherService));
    }

    protected override async Task<object> ExecuteFunctionAsync(object request)
    {
        if (request is WeatherRequest weatherRequest)
        {
            return await _weatherService.GetWeatherAsync(weatherRequest);
        }

        throw new ArgumentException("Invalid request type", nameof(request));
    }
}

public class WeatherRequest
{
    [JsonProperty("cities")]
    public string[] Cities { get; set; }
}

public class WeatherResponse
{
    public WeatherModel[] Models { get; set; }
}

public class WeatherModel
{
    public string Error { get; set; }
    public string City { get; set; }
    public double Temperature { get; set; }
    public double WindSpeed { get; set; }
    public string WeatherDescription { get; set; }

    public WeatherModel(string city, double temperature, double windSpeed, string weatherDescription)
    {
        City = city;
        Temperature = temperature;
        WindSpeed = windSpeed;
        WeatherDescription = weatherDescription;
    }

    // A method to parse the JSON data and create a WeatherModel instance
    public static WeatherModel FromJson(string json)
    {
        var jsonObject = JsonConvert.DeserializeObject<dynamic>(json);

        var city = (string)jsonObject["name"];
        var temperature = (double)jsonObject["main"]["temp"];
        var windSpeed = (double)jsonObject["wind"]["speed"];
        var weatherDescription = jsonObject["weather"][0]["description"] != null
                                 ? (string)jsonObject["weather"][0]["description"]
                                 : string.Empty;

        return new WeatherModel(city, temperature, windSpeed, weatherDescription);
    }

    public static WeatherModel CreateFailure(string city, string error)
    {
        return new WeatherModel(city, 0, 0, "unknown") { Error = error };
    }
}