#region License Information (GPL v3)

/*
    ShareX - A program developed by ShareX Team
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Input.Platform;
using ShareX.AvaloniaUI.Theming;

namespace ShareX.Tools;

public partial class CaptureTextWindow : Window
{
    private readonly CaptureTextViewModel _viewModel;
    private readonly AnalyzeImageRegionCaptureHandler _captureRegion;
    private readonly AIOptions _options;
    private readonly Action<AIOptions>? _optionsChanged;

    public CaptureTextWindow()
        : this(null, new AIOptions(), () => Task.FromResult<byte[]?>(null))
    {
    }

    public CaptureTextWindow(string? imagePath, AIOptions options, AnalyzeImageRegionCaptureHandler captureRegion,
        Action? playNotificationSound = null, Action<AIOptions>? optionsChanged = null)
    {
        _options = options;
        _optionsChanged = optionsChanged;
        _captureRegion = captureRegion;
        _viewModel = new CaptureTextViewModel(imagePath, options)
        {
            PlayNotificationSound = playNotificationSound
        };

        DataContext = _viewModel;
        InitializeComponent();
        RequestedThemeVariant = ThemeManager.GetCurrentTheme();

        _viewModel.SelectRegionRequested = SelectRegionAsync;
        _viewModel.CopyTextRequested = CopyTextAsync;
        _viewModel.EditOptionsRequested = EditOptionsAsync;
        _viewModel.OptionsChanged = () => _optionsChanged?.Invoke(_options);
        Opened += OnOpened;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        await _viewModel.InitializeAsync();
    }

    private async Task<byte[]?> SelectRegionAsync()
    {
        WindowState previousState = WindowState;
        try
        {
            WindowState = WindowState.Minimized;
            await Task.Delay(250);
            return await _captureRegion();
        }
        finally
        {
            WindowState = previousState;
            Activate();
        }
    }

    private async Task EditOptionsAsync()
    {
        AnalyzeImageOptionsWindow window = new(_options, new AnalyzeImageService());
        if (await window.ShowDialog<bool>(this))
        {
            _optionsChanged?.Invoke(_options);
        }
    }

    private async Task CopyTextAsync(string text)
    {
        if (Clipboard != null && !string.IsNullOrWhiteSpace(text))
        {
            await Clipboard.SetTextAsync(text);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
