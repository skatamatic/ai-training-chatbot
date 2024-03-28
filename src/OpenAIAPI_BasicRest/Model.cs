using Newtonsoft.Json;

namespace OpenAIAPI_BasicRest;

public class OpenAIResponse
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("object")]
    public string Object { get; set; }

    [JsonProperty("created")]
    public long Created { get; set; }

    [JsonProperty("model")]
    public string Model { get; set; }

    [JsonProperty("choices")]
    public List<Choice> Choices { get; set; }

    [JsonProperty("usage")]
    public Usage Usage { get; set; }

    [JsonProperty("system_fingerprint")]
    public string SystemFingerprint { get; set; }
}

public class Choice
{
    [JsonProperty("index")]
    public int Index { get; set; }

    [JsonProperty("message")]
    public ResponseMessage Message { get; set; }

    [JsonProperty("logprobs")]
    public object Logprobs { get; set; }

    [JsonProperty("finish_reason")]
    public string FinishReason { get; set; }
}

public class ResponseMessage
{
    [JsonProperty("role")]
    public string Role { get; set; }

    [JsonProperty("content")]
    public string Content { get; set; }
}

public class Usage
{
    [JsonProperty("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonProperty("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonProperty("total_tokens")]
    public int TotalTokens { get; set; }
}


public class OpenAIRequest
{
    [JsonProperty("model")]
    public string Model { get; set; }

    [JsonProperty("messages")]
    public List<RequestMessage> Messages { get; set; }

    [JsonProperty("max_tokens")]
    public int MaxTokens { get; set; } = 3000;
}

public class RequestMessage
{
    [JsonProperty("role")]
    public string Role { get; set; } = "user";

    [JsonProperty("content")]
    public List<Content> Content { get; set; }
}

public class Content
{
    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("text")]
    public string Text { get; set; }
}