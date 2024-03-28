using Rystem.OpenAi;
using ServiceInterface;

namespace OpenAIAPI_Rystem;

public class OpenAIRSystemAPI : IOpenAIAPI
{
    public string ActiveSessionId { get; private set; }

    private readonly IOpenAi _api;
    private readonly OpenAIConfig _config;

    private Dictionary<string, ChatSession> _sessions = new();

    public OpenAIRSystemAPI(IOpenAiFactory factory, OpenAIConfig config)
    {
        _config = config;
        _api = factory.Create();
    }

    public async Task<string> Prompt(string sessionId, string prompt)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            session = new ChatSession();
            _sessions.Add(sessionId, session);
        }

        ActiveSessionId = sessionId;

        session.AddUserPrompt(prompt);

        var request = session.BuildRequest(_api.Chat)
            .WithModel(_config.GetModelString())
            .SetMaxTokens(_config.MaxTokens);

        if (_config.EnableFunctions)
        {
            request = request.WithAllFunctions();
        }

        var result = await request.ExecuteAsync(_config.EnableFunctions);

        var resultContent = result?.Choices?.FirstOrDefault()?.Message?.Content ?? throw new FormatException("Failed to parse result");

        session.AddBotResult(resultContent);

        return resultContent;
    }
}
