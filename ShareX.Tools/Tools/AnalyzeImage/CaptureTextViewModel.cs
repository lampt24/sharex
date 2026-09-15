#region License Information (GPL v3)

/*
    ShareX - A program developed by ShareX Team
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ShareX.Tools;

public sealed record TranslationLanguage(string Name)
{
    public override string ToString() => Name;
}

public sealed partial class CaptureTextViewModel : ViewModelBase
{
    private readonly AIOptions _options;
    private readonly AnalyzeImageService _service;
    private byte[]? _imageData;
    private string? _imagePath;

    public IReadOnlyList<TranslationLanguage> Languages { get; } =
    [
        new("English"), new("Vietnamese"), new("Chinese (Simplified)"), new("Chinese (Traditional)"),
        new("Japanese"), new("Korean"), new("Thai"), new("Spanish"), new("French"), new("German"),
        new("Italian"), new("Portuguese"), new("Russian"), new("Ukrainian"), new("Arabic"), new("Hindi"),
        new("Indonesian"), new("Dutch"), new("Polish"), new("Turkish")
    ];

    [ObservableProperty]
    private TranslationLanguage _selectedLanguage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetectedText))]
    private string _detectedText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTranslation))]
    private string _translatedText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = string.Empty;

    public Func<Task<byte[]?>>? SelectRegionRequested { get; set; }
    public Func<string, Task>? CopyTextRequested { get; set; }
    public Func<Task>? EditOptionsRequested { get; set; }
    public Action? PlayNotificationSound { get; set; }
    public Action? OptionsChanged { get; set; }

    public bool HasDetectedText => !string.IsNullOrWhiteSpace(DetectedText);
    public bool HasTranslation => !string.IsNullOrWhiteSpace(TranslatedText);
    public bool IsIdle => !IsBusy;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public CaptureTextViewModel(string? imagePath, AIOptions options, AnalyzeImageService? service = null)
    {
        _imagePath = imagePath;
        _options = options;
        _service = service ?? new AnalyzeImageService();
        _selectedLanguage = Languages.FirstOrDefault(x =>
            x.Name.Equals(options.CaptureTextTranslationLanguage, StringComparison.OrdinalIgnoreCase)) ?? Languages[0];
    }

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_imagePath))
        {
            await SelectRegionAsync();
        }
        else
        {
            await DetectAndTranslateAsync();
        }
    }

    partial void OnSelectedLanguageChanged(TranslationLanguage value)
    {
        _options.CaptureTextTranslationLanguage = value.Name;
        OptionsChanged?.Invoke();

        if (HasDetectedText && !IsBusy)
        {
            _ = TranslateAsync();
        }
    }

    [RelayCommand]
    private async Task SelectRegionAsync()
    {
        if (IsBusy || SelectRegionRequested == null)
        {
            return;
        }

        byte[]? data = await SelectRegionRequested();
        if (data is { Length: > 0 })
        {
            _imagePath = null;
            _imageData = data;
            await DetectAndTranslateAsync();
        }
    }

    private async Task DetectAndTranslateAsync()
    {
        if (!_options.HasAPIKey)
        {
            ErrorMessage = Localization.Strings.AnalyzeImageViewModel_Configure_API_key;
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        DetectedText = Localization.Strings.AnalyzeImageViewModel_Thinking;
        TranslatedText = string.Empty;

        try
        {
            AIOptions detectOptions = _options.Clone();
            detectOptions.Input = CaptureTextOptions.Prompt;
            DetectedText = (await _service.AnalyzeAsync(_imagePath, _imageData, detectOptions)).ReplaceLineEndings("\r\n");
            await TranslateCoreAsync();
            PlayNotificationSound?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ToolsDiagnostics.ReportWarning(nameof(CaptureTextViewModel), "Capture text failed.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task TranslateAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            await TranslateCoreAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ToolsDiagnostics.ReportWarning(nameof(CaptureTextViewModel), "Text translation failed.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task TranslateCoreAsync()
    {
        if (!HasDetectedText)
        {
            return;
        }

        TranslatedText = "Translating...";
        AIOptions translateOptions = _options.Clone();
        translateOptions.Input = $"Translate the text below into {SelectedLanguage.Name}. Return only the translation, preserving line breaks.\n\n{DetectedText}";
        TranslatedText = (await _service.AnalyzeAsync(_imagePath, _imageData, translateOptions)).ReplaceLineEndings("\r\n");
    }

    [RelayCommand]
    private Task CopyDetectedTextAsync() => CopyTextRequested?.Invoke(DetectedText) ?? Task.CompletedTask;

    [RelayCommand]
    private Task CopyTranslationAsync() => CopyTextRequested?.Invoke(TranslatedText) ?? Task.CompletedTask;

    [RelayCommand]
    private Task EditOptionsAsync() => EditOptionsRequested?.Invoke() ?? Task.CompletedTask;
}
