#region License Information (GPL v3)

/*
    ShareX - A program developed by ShareX Team
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

using System.Text;
using System.Text.Json;

namespace ShareX.Tools;

public static class OpenAIResponseParser
{
    public static bool IsChatCompletionResponse(string response)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(response);
            JsonElement root = document.RootElement;
            return root.TryGetProperty("object", out JsonElement type) && type.GetString() == "chat.completion";
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string ParseResponseText(string response)
    {
        return Parse(response, ExtractResponseText, "response.output_text.delta");
    }

    public static string ParseChatCompletionText(string response)
    {
        return Parse(response, ExtractChatCompletionText, null);
    }

    private static string Parse(string response, Func<JsonElement, string> extractFinalText, string? deltaEventType)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return string.Empty;
        }

        string trimmedResponse = response.TrimStart();
        if (!trimmedResponse.StartsWith("data:", StringComparison.Ordinal))
        {
            using JsonDocument document = JsonDocument.Parse(response);
            string finalText = extractFinalText(document.RootElement);
            return string.IsNullOrEmpty(finalText) && deltaEventType != null
                ? ExtractChatCompletionText(document.RootElement)
                : finalText;
        }

        StringBuilder text = new();
        foreach (string line in response.Split('\n'))
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            string payload = line[5..].Trim();
            if (payload.Length == 0 || payload == "[DONE]")
            {
                continue;
            }

            using JsonDocument document = JsonDocument.Parse(payload);
            JsonElement root = document.RootElement;

            if (deltaEventType != null &&
                root.TryGetProperty("type", out JsonElement type) &&
                type.GetString() == deltaEventType &&
                root.TryGetProperty("delta", out JsonElement delta))
            {
                text.Append(delta.GetString());
                continue;
            }

            string finalText = extractFinalText(root);
            if (text.Length == 0 && !string.IsNullOrEmpty(finalText))
            {
                text.Append(finalText);
            }
        }

        return text.ToString();
    }

    private static string ExtractResponseText(JsonElement root)
    {
        if (root.TryGetProperty("response", out JsonElement response))
        {
            root = response;
        }

        if (!root.TryGetProperty("output", out JsonElement output) || output.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        StringBuilder text = new();
        foreach (JsonElement item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out JsonElement content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement part in content.EnumerateArray())
            {
                if (part.TryGetProperty("text", out JsonElement value))
                {
                    text.Append(value.GetString());
                }
            }
        }

        return text.ToString();
    }

    private static string ExtractChatCompletionText(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out JsonElement choices) || choices.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        StringBuilder text = new();
        foreach (JsonElement choice in choices.EnumerateArray())
        {
            if (choice.TryGetProperty("delta", out JsonElement delta) && delta.TryGetProperty("content", out JsonElement deltaContent))
            {
                text.Append(deltaContent.GetString());
            }
            else if (choice.TryGetProperty("message", out JsonElement message) && message.TryGetProperty("content", out JsonElement messageContent))
            {
                text.Append(messageContent.GetString());
            }
        }

        return text.ToString();
    }
}
