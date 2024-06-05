using Newtonsoft.Json;
using Shared;
using System.Text;

namespace OpenAIAPI_BasicRest;

public class RestOpenAIAPI : IOpenAIAPI
{
    private const string ENDPOINT = "https://api.openai.com/v1/chat/completions";

    private readonly HttpClient _client;
    private readonly OpenAIConfig _config;

    public string ActiveSessionId => "???";

    public string SystemPrompt { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public RestOpenAIAPI(OpenAIConfig config)
    {
        _config = config;

        _client = new HttpClient();
        _client.DefaultRequestHeaders.Add("Authorization", "Bearer " + _config.ApiKey);
    }

    public async Task<string> Prompt(string prompt)
    {
        var jsonPayload = CreateRequestJson(prompt);

        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        HttpResponseMessage response = await _client.PostAsync(ENDPOINT, content);

        response.EnsureSuccessStatusCode();

        var responseObject = JsonConvert.DeserializeObject<OpenAIResponse>(await response.Content.ReadAsStringAsync());

        return responseObject?.Choices?.FirstOrDefault()?.Message?.Content ?? throw new FormatException("Failed to parse response");
    }

    private string CreateRequestJson(string textContent)
    {
        var request = new OpenAIRequest
        {
            Model = _config.GetModelString(),
            Messages = new List<RequestMessage>
        {
            new RequestMessage
            {
                Content = new List<Content>
                {
                    new Content { Type = "text", Text = textContent }
                }
            }
        },
            MaxTokens = _config.MaxTokens
        };

        string jsonPayload = JsonConvert.SerializeObject(request);
        return jsonPayload;
    }

    public Task<string> Prompt(string sessionId, string prompt) => Prompt(prompt);
}
