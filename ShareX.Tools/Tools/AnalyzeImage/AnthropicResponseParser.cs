#region License Information (GPL v3)

/*
    ShareX - A program developed by ShareX Team
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information

using System.Text;
using System.Text.Json;

namespace ShareX.Tools;

public static class AnthropicResponseParser
{
    public static string ParseMessageText(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return string.Empty;
        }

        if (!response.TrimStart().StartsWith("data:", StringComparison.Ordinal))
        {
            using JsonDocument document = JsonDocument.Parse(response);
            return ExtractContentText(document.RootElement);
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
            if (root.TryGetProperty("type", out JsonElement type) && type.GetString() == "content_block_delta" &&
                root.TryGetProperty("delta", out JsonElement delta) && delta.TryGetProperty("text", out JsonElement deltaText))
            {
                text.Append(deltaText.GetString());
            }
            else if (text.Length == 0)
            {
                text.Append(ExtractContentText(root));
            }
        }

        return text.ToString();
    }

    private static string ExtractContentText(JsonElement root)
    {
        if (!root.TryGetProperty("content", out JsonElement content) || content.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        StringBuilder text = new();
        foreach (JsonElement part in content.EnumerateArray())
        {
            if (part.TryGetProperty("type", out JsonElement type) && type.GetString() == "text" &&
                part.TryGetProperty("text", out JsonElement value))
            {
                text.Append(value.GetString());
            }
        }

        return text.ToString();
    }
}
