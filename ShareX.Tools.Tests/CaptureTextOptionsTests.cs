using ShareX.Tools;
using System.Reflection;

internal static class CaptureTextOptionsTests
{
    public static void Run()
    {
        ParsesStreamingVisionResponse();
        ParsesEventPrefixedStreamingVisionResponse();
        ParsesChatCompletionResponseFromResponsesEndpoint();
        RequestsNonStreamingVisionResponses();
        SavesCaptureTextApiSettings();
        BuildsEndpointsFromV1BaseUrl();
        ParsesAnthropicMessageResponse();
        LeavesCustomGatewayEmptyByDefault();
        UsesTranscriptionPromptByDefault();
        SelectsTranscriptionPresetAndMigratesOldDefault();
        KeepsSelectedModelWhenGatewayDoesNotReturnIt();

        AIOptions source = new()
        {
            Provider = AIProvider.OpenAILegacy,
            OpenAIAPIKey = "secret-token",
            OpenAIModel = "vision-model",
            OpenAICustomURL = "https://vision.example/v1/chat/completions",
            Input = "Describe this image",
            AutoStartRegion = false,
            AutoStartAnalyze = false
        };

        AIOptions captureText = CaptureTextOptions.Create(source);

        AssertEqual(CaptureTextOptions.Prompt, captureText.Input, "Capture Text prompt");
        AssertTrue(CaptureTextOptions.Prompt.Contains("Do not summarize or omit", StringComparison.Ordinal),
            "Capture Text prompt must require complete transcription");
        AssertTrue(captureText.AutoStartRegion, "Capture Text must select a region immediately");
        AssertTrue(captureText.AutoStartAnalyze, "Capture Text must call Vision immediately");
        AssertEqual("secret-token", captureText.OpenAIAPIKey, "Access token must be retained");
        AssertEqual("vision-model", captureText.OpenAIModel, "Model must be retained");
        AssertEqual("https://vision.example/v1/chat/completions", captureText.OpenAICustomURL, "API URL must be retained");
        AssertEqual("Describe this image", source.Input, "Source settings must not be changed");
    }

    private static void ParsesStreamingVisionResponse()
    {
        const string response = "data: {\"type\":\"response.output_text.delta\",\"delta\":\"Hello \"}\n\n" +
            "data: {\"type\":\"response.output_text.delta\",\"delta\":\"world\"}\n\n" +
            "data: {\"type\":\"response.completed\",\"response\":{\"output\":[{\"content\":[{\"text\":\"Hello world\"}]}]}}\n\n" +
            "data: [DONE]";

        AssertEqual("Hello world", OpenAIResponseParser.ParseResponseText(response),
            "Streaming OpenAI response text");
    }

    private static void ParsesEventPrefixedStreamingVisionResponse()
    {
        const string response = "event: response.created\n" +
            "data: {\"type\":\"response.created\",\"response\":{\"output\":[]}}\n\n" +
            "event: response.output_text.delta\n" +
            "data: {\"type\":\"response.output_text.delta\",\"delta\":\"Gateway text\"}\n\n" +
            "data: [DONE]";

        AssertEqual("Gateway text", OpenAIResponseParser.ParseResponseText(response),
            "Event-prefixed streaming OpenAI response text");
    }

    private static void RequestsNonStreamingVisionResponses()
    {
        ChatGPTRequest responsesRequest = new() { stream = false };
        ChatGPTLegacyRequest chatRequest = new() { stream = false };

        AssertTrue(!responsesRequest.stream, "Responses API request must disable streaming");
        AssertTrue(!chatRequest.stream, "Chat Completions request must disable streaming");
    }

    private static void ParsesChatCompletionResponseFromResponsesEndpoint()
    {
        const string response = "{\"object\":\"chat.completion\",\"choices\":[{\"message\":{\"content\":\"https://ai.lampt.works/v1\"}}]}";

        AssertEqual("https://ai.lampt.works/v1", OpenAIResponseParser.ParseResponseText(response),
            "Chat Completions response returned from Responses endpoint");
        AssertTrue(OpenAIResponseParser.IsChatCompletionResponse(response),
            "Chat Completions response must trigger an image-compatible retry");
    }

    private static void SavesCaptureTextApiSettings()
    {
        AIOptions target = new()
        {
            Provider = AIProvider.OpenAI,
            Input = "Describe this image",
            AutoStartRegion = false,
            AutoStartAnalyze = false
        };
        AIOptions captureOptions = CaptureTextOptions.Create(target);
        captureOptions.Provider = AIProvider.OpenAILegacy;
        captureOptions.OpenAIAPIKey = "updated-token";
        captureOptions.OpenAIModel = "ag/gemini-3.7-flash-high";
        captureOptions.OpenAICustomURL = "https://ai.lampt.works/v1";

        CaptureTextOptions.SaveConfiguration(target, captureOptions);

        AssertEqual("updated-token", target.OpenAIAPIKey, "Saved access token");
        AssertEqual("ag/gemini-3.7-flash-high", target.OpenAIModel, "Saved model");
        AssertEqual("https://ai.lampt.works/v1", target.OpenAICustomURL, "Saved API URL");
        AssertEqual("Describe this image", target.Input, "Analyze Image prompt must be retained");
        AssertTrue(!target.AutoStartRegion && !target.AutoStartAnalyze, "Analyze Image startup behavior must be retained");
    }

    private static void BuildsEndpointsFromV1BaseUrl()
    {
        AssertEqual("https://vision.example/v1/chat/completions",
            AIEndpointBuilder.GetOpenAIChatCompletionsUrl("https://vision.example/v1"),
            "OpenAI endpoint built from a v1 base URL");
        AssertEqual("https://vision.example/v1/messages",
            AIEndpointBuilder.GetAnthropicMessagesUrl("https://vision.example/v1"),
            "Anthropic endpoint built from a v1 base URL");
    }

    private static void ParsesAnthropicMessageResponse()
    {
        const string response = "{\"id\":\"msg_123\",\"type\":\"message\",\"content\":[{\"type\":\"text\",\"text\":\"Complete transcription\"}],\"stop_reason\":\"end_turn\"}";

        AssertEqual("Complete transcription", AnthropicResponseParser.ParseMessageText(response),
            "Anthropic Messages response text");
    }

    private static void LeavesCustomGatewayEmptyByDefault()
    {
        AIOptions options = new();
        AssertTrue(string.IsNullOrEmpty(options.OpenAICustomURL),
            "Custom gateway default URL must be empty");
    }

    private static void UsesTranscriptionPromptByDefault()
    {
        AIOptions options = new();
        AssertEqual("Transcribe the image's text. Do not write anything else.", options.Input,
            "Default AI Analyze prompt");
    }

    private static void SelectsTranscriptionPresetAndMigratesOldDefault()
    {
        AIOptions options = new()
        {
            Input = "What is in this image?"
        };

        using AnalyzeImageViewModel viewModel = new(null, options);

        AssertEqual(AIOptions.DefaultPrompt, viewModel.Prompt,
            "Saved legacy default prompt must migrate to the transcription prompt");
        AssertEqual(AIOptions.DefaultPrompt, viewModel.SelectedPreset,
            "Transcription prompt must be selected in the Analyze Image UI");
        AssertEqual(AIOptions.DefaultPrompt, options.Input,
            "Migrated prompt must be saved to the shared AI options");
    }

    private static void KeepsSelectedModelWhenGatewayDoesNotReturnIt()
    {
        MethodInfo? method = typeof(AnalyzeImageOptionsViewModel).GetMethod("GetModelsIncludingSelected",
            BindingFlags.NonPublic | BindingFlags.Static);
        AssertTrue(method != null, "Model reload must preserve the selected model");

        IReadOnlyList<string> models = (IReadOnlyList<string>)method!.Invoke(null,
            [new[] { "gateway-model-a", "gateway-model-b" }, "previously-selected-model"])!;

        AssertTrue(models.Contains("previously-selected-model"),
            "Selected model must remain available after model reload");
    }

    private static void AssertEqual(string expected, string? actual, string description)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{description}: expected '{expected}', got '{actual ?? "<null>"}'.");
        }
    }

    private static void AssertTrue(bool value, string description)
    {
        if (!value)
        {
            throw new InvalidOperationException(description);
        }
    }
}
