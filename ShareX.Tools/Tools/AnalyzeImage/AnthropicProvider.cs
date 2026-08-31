#region License Information (GPL v3)

/*
    ShareX - A program developed by ShareX Team
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information

using ShareX.HelpersLib;
using System.Drawing;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ShareX.Tools;

public sealed class AnthropicProvider : IAIProvider
{
    private const string AnthropicVersion = "2023-06-01";

    private readonly string _apiKey;
    private readonly string _model;
    private readonly string? _customUrl;

    public AnthropicProvider(string apiKey, string model, string? customUrl = null)
    {
        _apiKey = apiKey;
        _model = model;
        _customUrl = customUrl;
    }

    public async Task<string> AnalyzeImage(string filePath, string prompt, string reasoningEffort, string verbosity)
    {
        using Image image = ImageHelpers.LoadImage(filePath);
        return await AnalyzeImage(image, prompt, reasoningEffort, verbosity);
    }

    public async Task<string> AnalyzeImage(Image image, string prompt, string reasoningEffort, string verbosity)
    {
        using MemoryStream stream = new();
        ImageHelpers.SaveJPEG(image, stream, 90);
        string base64Image = Convert.ToBase64String(stream.ToArray());

        if (string.IsNullOrWhiteSpace(prompt))
        {
            prompt = AIOptions.DefaultPrompt;
        }

        object request = new
        {
            model = _model,
            max_tokens = 8192,
            stream = false,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = prompt },
                        new
                        {
                            type = "image",
                            source = new { type = "base64", media_type = "image/jpeg", data = base64Image }
                        }
                    }
                }
            }
        };

        using HttpClient httpClient = HttpClientFactory.Create();
        httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        httpClient.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        string json = JsonSerializer.Serialize(request);
        using StringContent content = new(json, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await httpClient.PostAsync(AIEndpointBuilder.GetAnthropicMessagesUrl(_customUrl), content);
        string responseString = await response.Content.ReadAsStringAsync();
        DebugHelper.WriteLine($"[{nameof(AnthropicProvider)}] Vision response ({(int)response.StatusCode}): {responseString}");
        response.EnsureSuccessStatusCode();

        return AnthropicResponseParser.ParseMessageText(responseString);
    }
}
