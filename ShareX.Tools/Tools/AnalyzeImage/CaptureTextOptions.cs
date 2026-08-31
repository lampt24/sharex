#region License Information (GPL v3)

/*
    ShareX - A program developed by ShareX Team
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

namespace ShareX.Tools;

public static class CaptureTextOptions
{
    public const string Prompt = "Transcribe every visible character in this image exactly. Do not summarize or omit text. Preserve line breaks.";

    public static AIOptions Create(AIOptions source)
    {
        ArgumentNullException.ThrowIfNull(source);

        AIOptions options = source.Clone();
        options.Input = Prompt;
        options.AutoStartRegion = true;
        options.AutoStartAnalyze = true;
        return options;
    }

    public static void SaveConfiguration(AIOptions target, AIOptions captureTextOptions)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(captureTextOptions);

        string input = target.Input;
        bool autoStartRegion = target.AutoStartRegion;
        bool autoStartAnalyze = target.AutoStartAnalyze;
        target.CopyFrom(captureTextOptions);
        target.Input = input;
        target.AutoStartRegion = autoStartRegion;
        target.AutoStartAnalyze = autoStartAnalyze;
    }
}
