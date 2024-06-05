namespace Shared;

public class OpenWeatherConfig
{
    public string ApiKey { get; }
    
    public OpenWeatherConfig(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key is required", nameof(apiKey));

        ApiKey = apiKey;
    }
}