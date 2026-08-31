#region License Information (GPL v3)

/*
    ShareX - A program developed by ShareX Team
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information

namespace ShareX.Tools;

public static class AIEndpointBuilder
{
    public static string GetOpenAIResponsesUrl(string? baseUrl) => GetEndpoint(baseUrl, "responses", "https://api.openai.com");

    public static string GetOpenAIChatCompletionsUrl(string? baseUrl) => GetEndpoint(baseUrl, "chat/completions", "https://api.openai.com");

    public static string GetOpenAIModelsUrl(string? baseUrl) => GetEndpoint(baseUrl, "models", "https://api.openai.com");

    public static string GetVisionModelsUrl(string? baseUrl) => GetEndpoint(baseUrl, "models/image-to-text", "https://api.openai.com");

    public static string GetAnthropicMessagesUrl(string? baseUrl) => GetEndpoint(baseUrl, "messages", "https://api.anthropic.com");

    private static string GetEndpoint(string? baseUrl, string endpoint, string defaultBaseUrl)
    {
        string url = string.IsNullOrWhiteSpace(baseUrl) ? defaultBaseUrl : baseUrl.Trim();
        url = url.TrimEnd('/');

        string endpointSuffix = "/" + endpoint;
        if (url.EndsWith(endpointSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        return url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? url + endpointSuffix
            : url + "/v1" + endpointSuffix;
    }
}
