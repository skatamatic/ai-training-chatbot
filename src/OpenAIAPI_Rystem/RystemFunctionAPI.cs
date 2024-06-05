using Newtonsoft.Json;
using OpenAIAPI_Rystem.Functions;
using Rystem.OpenAi;
using Rystem.OpenAi.Chat;
using Shared;

namespace OpenAIAPI_Rystem;

public class RystemFunctionAPI : IOpenAIAPI
{
    private readonly IOpenAi _api;
    private readonly OpenAIConfig _config;

    private Dictionary<string, ChatSession> _sessions = new();
    private Dictionary<string, IOpenAiChatFunction> _chatFunctions = new();

    public string ActiveSessionId { get; private set; }
    public string SystemPrompt { get; set; }

    public RystemFunctionAPI(IOpenAiFactory factory, OpenAIConfig config, IEnumerable<IOpenAiChatFunction> chatFunctions)
    {
        _config = config;
        _api = factory.Create();

        foreach (var function in chatFunctions)
        {
            _chatFunctions[function.Name] = function;

            //Some functions (namely the Function Chain) need access to the API to do their own prompting.
            //But the API needs access to all the functions
            //So do a late binding here to skirt around the circular dependency
            if (function is IPromptingFunction prompter)
            {
                prompter.InstallApi(this);
            }
        }
    }

    private static ChatMessageFunction GetFunctionMessage(ChatResult result) => result.Choices.FirstOrDefault()?.Message?.Function;
    private static bool IsFunctionCompletion(ChatResult result) => result.Choices.FirstOrDefault()?.Message?.Function?.Name != null;
    private static string GetContentFromResult(ChatResult result) => result?.Choices?.FirstOrDefault()?.Message?.Content ?? throw new FormatException("Failed to parse result");

    public async Task<string> Prompt(string sessionId, string prompt)
    {
        ActiveSessionId = sessionId;

        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            session = new ChatSession();
            _sessions.Add(sessionId, session);
        }

        session.AddUserPrompt(prompt);

        var request = session.BuildRequest(_api.Chat)
            .WithModel(_config.GetModelString())
            .SetMaxTokens(_config.MaxTokens);

        if (_config.EnableFunctions && _chatFunctions.Count > 0)
        {
            request = request.WithAllFunctions();
        }

        if (!string.IsNullOrEmpty(_config.SystemPrompt))
        {
            request.AddSystemMessage(_config.SystemPrompt);
        }

        if (!string.IsNullOrEmpty(SystemPrompt))
        {
            request.AddSystemMessage(SystemPrompt);
        }

        var result = await request.ExecuteAsync(false);

        while (IsFunctionCompletion(result))
        {
            ChatMessageFunction chatFunction = GetFunctionMessage(result);
            IOpenAiChatFunction function = _chatFunctions[chatFunction.Name];

            string functionResult = JsonConvert.SerializeObject(await function.WrapAsync(chatFunction.Arguments));

            //These can be extremely large (like if a file was read)
            //Because of this, it is probably best to usually not store actual function call results in session history
            if (_config.TransmitFunctionResults)
                session.AddFunctionResult(function.Name, functionResult);

            request.AddFunctionMessage(function.Name, functionResult)
                .WithNumberOfChoicesPerPrompt(1);

            result = await request.ExecuteAsync(false);
        }

        var resultContent = GetContentFromResult(result);

        session.AddBotResult(resultContent);

        return resultContent; 
    }
}
