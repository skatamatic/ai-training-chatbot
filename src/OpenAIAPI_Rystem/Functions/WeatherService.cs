using Shared;

namespace OpenAIAPI_Rystem.Functions;

public interface IWeatherService
{
    Task<WeatherResponse> GetWeatherAsync(WeatherRequest request);
}

public class WeatherService : IWeatherService
{
    private const string BaseUrl = "https://api.openweathermap.org/data/2.5/weather";

    private OpenWeatherConfig _config;

    public WeatherService(OpenWeatherConfig config)
    {
        _config = config;
    }

    public async Task<WeatherResponse> GetWeatherAsync(WeatherRequest request)
    {
        using var client = new HttpClient();

        List<WeatherModel> models = new();

        foreach (var city in request.Cities)
        {
            try
            {
                var url = $"{BaseUrl}?q={city}&appid={_config.ApiKey}&units=metric";
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();
                models.Add(WeatherModel.FromJson(jsonString));
            }
            catch (Exception ex)
            {
                models.Add(WeatherModel.CreateFailure(city, ex.Message));
            }
        }

        return new WeatherResponse() { Models = models.ToArray() };
    }
}