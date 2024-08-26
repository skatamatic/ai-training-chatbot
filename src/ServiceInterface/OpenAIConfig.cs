namespace Shared;

public class OpenAIConfig
{
    public string ApiKey { get; }
    public ModelType Model { get; set; } = ModelType.GPT3_5_Turbo;
    public string Mode { get; set; } = "completions";
    public int MaxTokens { get; set; } = 100;
    public bool EnableFunctions { get; set; } = false;
    public bool TransmitFunctionResults { get; set; } = false;
    public string SystemPrompt { get; set; }

    public OpenAIConfig(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key is required", nameof(apiKey));

        ApiKey = apiKey;
    }

    public string GetModelString()
    {
        return Model switch
        {
            ModelType.GPT4_ImagePreview => "gpt-4-vision-preview",
            ModelType.GPT4_Turbo => "gpt-4-turbo-preview",
            ModelType.GPT3_5_Turbo => "gpt-3.5-turbo",
            ModelType.GPT4o => "gpt-4o-2024-08-06",
            ModelType.GPT4oMini => "gpt-4o-mini",
            _ => throw new ArgumentOutOfRangeException(nameof(Model), "Unsupported model type")
        };
    }
}