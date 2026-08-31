#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

#nullable disable

using ShareX.HelpersLib;

namespace ShareX.Tools
{
    public enum AIProvider
    {
        OpenAI,
        Gemini,
        OpenRouter,
        OpenAILegacy,
        Anthropic
    }

    public class AIOptions
    {
        public AIProvider Provider { get; set; } = AIProvider.OpenAI;

        [JsonEncrypt]
        public string OpenAIAPIKey { get; set; }
        public string OpenAIModel { get; set; } = "gpt-5-mini";
        public string OpenAICustomURL { get; set; }
        public string OpenAIReasoningEffort { get; set; } = "minimal";
        public string OpenAIVerbosity { get; set; } = "medium";

        [JsonEncrypt]
        public string AnthropicAPIKey { get; set; }
        public string AnthropicModel { get; set; } = "claude-sonnet-4-5";
        public string AnthropicCustomURL { get; set; } = "https://api.anthropic.com/v1";

        [JsonEncrypt]
        public string GeminiAPIKey { get; set; }
        public string GeminiModel { get; set; } = "gemini-1.5-flash-latest";

        [JsonEncrypt]
        public string OpenRouterAPIKey { get; set; }
        public string OpenRouterModel { get; set; } = "google/gemini-flash-1.5";

        public string Input { get; set; } = Localization.Strings.AIOptions_What_is_in_this_image;
        public bool AutoStartRegion { get; set; } = true;
        public bool AutoStartAnalyze { get; set; } = true;
        public bool AutoCopyResult { get; set; } = false;

        public bool HasAPIKey => !string.IsNullOrWhiteSpace(OpenAIAPIKey);

        public AIOptions Clone() => new AIOptions
        {
            Provider = Provider,
            OpenAIAPIKey = OpenAIAPIKey,
            OpenAIModel = OpenAIModel,
            OpenAICustomURL = OpenAICustomURL,
            OpenAIReasoningEffort = OpenAIReasoningEffort,
            OpenAIVerbosity = OpenAIVerbosity,
            AnthropicAPIKey = AnthropicAPIKey,
            AnthropicModel = AnthropicModel,
            AnthropicCustomURL = AnthropicCustomURL,
            GeminiAPIKey = GeminiAPIKey,
            GeminiModel = GeminiModel,
            OpenRouterAPIKey = OpenRouterAPIKey,
            OpenRouterModel = OpenRouterModel,
            Input = Input,
            AutoStartRegion = AutoStartRegion,
            AutoStartAnalyze = AutoStartAnalyze,
            AutoCopyResult = AutoCopyResult
        };

        public void CopyFrom(AIOptions source)
        {
            Provider = source.Provider;
            OpenAIAPIKey = source.OpenAIAPIKey;
            OpenAIModel = source.OpenAIModel;
            OpenAICustomURL = source.OpenAICustomURL;
            OpenAIReasoningEffort = source.OpenAIReasoningEffort;
            OpenAIVerbosity = source.OpenAIVerbosity;
            AnthropicAPIKey = source.AnthropicAPIKey;
            AnthropicModel = source.AnthropicModel;
            AnthropicCustomURL = source.AnthropicCustomURL;
            GeminiAPIKey = source.GeminiAPIKey;
            GeminiModel = source.GeminiModel;
            OpenRouterAPIKey = source.OpenRouterAPIKey;
            OpenRouterModel = source.OpenRouterModel;
            Input = source.Input;
            AutoStartRegion = source.AutoStartRegion;
            AutoStartAnalyze = source.AutoStartAnalyze;
            AutoCopyResult = source.AutoCopyResult;
        }
    }
}
