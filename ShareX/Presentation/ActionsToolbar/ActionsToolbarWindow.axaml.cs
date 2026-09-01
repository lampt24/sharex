#region License Information (GPL v3)

/*
    ShareX - A program developed by ShareX Team
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

#nullable enable

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ShareX.AvaloniaUI.Theming;
using ShareX.HelpersLib;
using ShareX.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using DrawingPoint = System.Drawing.Point;
using FormsDataFormats = System.Windows.Forms.DataFormats;
using FormsDataObject = System.Windows.Forms.DataObject;

namespace ShareX;

public partial class ActionsToolbarWindow : Window
{
    private Button? _showCursorButton;
    private Button? _clipboardToggleButton;
    private TextBlock? _clipboardToggleIcon;
    private TextBlock? _delayText;
    private bool _positionReady;
    private bool _adjustingPosition;
    private bool _closing;

    public ActionsToolbarWindow()
    {
        InitializeComponent();
        RequestedThemeVariant = ThemeManager.GetCurrentTheme();
        Topmost = Program.Settings.ActionsToolbarStayTopMost;
        Program.Settings.ActionsToolbarList ??= [];
        MigrateLegacyDefaultActions();

        ToolTip.SetTip(TitleHandle, Strings.ActionsToolbarWindow_Tip);
        ToolTip.SetPlacement(TitleHandle, PlacementMode.Top);
        ToolTip.SetVerticalOffset(TitleHandle, -4);
        ToolTip.SetShowDelay(TitleHandle, 400);
        ToolTip.SetBetweenShowDelay(TitleHandle, 100);
        TitleHandle.ContextMenu = CreateToolbarMenu();
        UpdateTitleCursor();
        RefreshToolbar();

        Opened += OnOpened;
        PositionChanged += OnPositionChanged;
        Closed += (_, _) => _closing = true;
        PropertyChanged += OnWindowPropertyChanged;
    }

    private static void MigrateLegacyDefaultActions()
    {
        HotkeyType[] legacyDefault =
        [
            HotkeyType.RectangleRegion,
            HotkeyType.PrintScreen,
            HotkeyType.ScreenRecorder,
            HotkeyType.None,
            HotkeyType.FileUpload,
            HotkeyType.ClipboardUploadWithContentViewer
        ];

        if (Program.Settings.ActionsToolbarList.SequenceEqual(legacyDefault))
        {
            Program.Settings.ActionsToolbarList =
            [
                HotkeyType.PrintScreen,
                HotkeyType.ActiveWindow,
                HotkeyType.ActiveMonitor,
                HotkeyType.RectangleRegion,
                HotkeyType.LastRegion,
                HotkeyType.ScreenRecorder,
                HotkeyType.ScreenRecorderGIF,
                HotkeyType.ScrollingCapture,
                HotkeyType.HorizontalScrollingCapture,
                HotkeyType.AutoCapture
            ];
        }

        int scrollingCaptureIndex = Program.Settings.ActionsToolbarList.IndexOf(HotkeyType.ScrollingCapture);
        if (scrollingCaptureIndex >= 0 &&
            !Program.Settings.ActionsToolbarList.Contains(HotkeyType.HorizontalScrollingCapture))
        {
            Program.Settings.ActionsToolbarList.Insert(scrollingCaptureIndex + 1, HotkeyType.HorizontalScrollingCapture);
        }
    }

    internal void RefreshToolbar()
    {
        while (ToolbarItems.Children.Count > 1)
        {
            ToolbarItems.Children.RemoveAt(1);
        }

        foreach (HotkeyType action in Program.Settings.ActionsToolbarList)
        {
            if (action == HotkeyType.None)
            {
                ToolbarItems.Children.Add(new Border
                {
                    Width = 1,
                    Height = 22,
                    Margin = new Thickness(3, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = this.FindResource("ShareX.Brush.Border") as Avalonia.Media.IBrush
                });
                continue;
            }

            TextBlock icon = new()
            {
                Text = TaskHelpers.FindMenuLucideIcon(action),
                FontSize = 17,
                TextAlignment = Avalonia.Media.TextAlignment.Center,
                Foreground = this.FindResource("ShareX.Brush.Accent") as Avalonia.Media.IBrush,
                IsHitTestVisible = false
            };
            icon.Classes.Add("icon");

            Button button = new()
            {
                Content = icon,
                Tag = action
            };
            button.Classes.Add("toolbar-action");
            ToolTip.SetTip(button, action.GetLocalizedDescription());
            ToolTip.SetPlacement(button, PlacementMode.Top);
            ToolTip.SetVerticalOffset(button, -4);
            ToolTip.SetShowDelay(button, 400);
            ToolTip.SetBetweenShowDelay(button, 100);
            button.Click += OnActionClick;
            ToolbarItems.Children.Add(button);
        }

        AddCaptureOptions();

        Dispatcher.UIThread.Post(ClampAndSavePosition, DispatcherPriority.Loaded);
    }

    private void AddCaptureOptions()
    {
        ToolbarItems.Children.Add(new Border
        {
            Width = 1,
            Height = 22,
            Margin = new Thickness(3, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = this.FindResource("ShareX.Brush.Border") as Avalonia.Media.IBrush
        });

        _showCursorButton = CreateCaptureOptionButton(LucideIcons.mouse_pointer_2, Strings.MainMenuBuilder_ShowCursor);
        _showCursorButton.Click += (_, _) =>
        {
            Program.DefaultTaskSettings.CaptureSettings.ShowCursor =
                !Program.DefaultTaskSettings.CaptureSettings.ShowCursor;
            UpdateCaptureOptions();
            SaveSettings();
        };
        ToolbarItems.Children.Add(_showCursorButton);

        _clipboardToggleIcon = new TextBlock
        {
            FontSize = 17,
            TextAlignment = Avalonia.Media.TextAlignment.Center,
            Foreground = this.FindResource("ShareX.Brush.Accent") as Avalonia.Media.IBrush,
            IsHitTestVisible = false
        };
        _clipboardToggleIcon.Classes.Add("icon");
        _clipboardToggleButton = new Button { Content = _clipboardToggleIcon };
        _clipboardToggleButton.Classes.Add("toolbar-action");
        ToolTip.SetPlacement(_clipboardToggleButton, PlacementMode.Top);
        ToolTip.SetShowDelay(_clipboardToggleButton, 400);
        _clipboardToggleButton.Click += (_, _) =>
        {
            // Toggle: CopyImage ↔ CopyFilePath
            bool wasImage = Program.DefaultTaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.CopyImageToClipboard);
            Program.DefaultTaskSettings.AfterCaptureJob &= ~(AfterCaptureTasks.CopyImageToClipboard | AfterCaptureTasks.CopyFilePathToClipboard);
            if (wasImage)
            {
                Program.DefaultTaskSettings.AfterCaptureJob |= AfterCaptureTasks.CopyFilePathToClipboard;
            }
            else
            {
                Program.DefaultTaskSettings.AfterCaptureJob |= AfterCaptureTasks.CopyImageToClipboard;
            }
            UpdateCaptureOptions();
            SaveSettings();
        };
        ToolbarItems.Children.Add(_clipboardToggleButton);

        _delayText = new TextBlock
        {
            FontSize = 11,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsHitTestVisible = false
        };
        Button delayButton = new() { Content = _delayText, Width = 42, MinWidth = 42 };
        delayButton.Classes.Add("toolbar-action");
        ToolTip.SetTip(delayButton, string.Format(Strings.ScreenshotDelay0S,
            Program.DefaultTaskSettings.CaptureSettings.ScreenshotDelay.ToString("0.#")));
        delayButton.Click += (_, _) => ShowDelayMenu(delayButton);
        ToolbarItems.Children.Add(delayButton);

        UpdateCaptureOptions();
    }

    private Button CreateCaptureOptionButton(string iconText, string tooltip)
    {
        TextBlock icon = new()
        {
            Text = iconText,
            FontSize = 17,
            TextAlignment = Avalonia.Media.TextAlignment.Center,
            Foreground = this.FindResource("ShareX.Brush.Accent") as Avalonia.Media.IBrush,
            IsHitTestVisible = false
        };
        icon.Classes.Add("icon");

        Button button = new() { Content = icon };
        button.Classes.Add("toolbar-action");
        ToolTip.SetTip(button, tooltip);
        ToolTip.SetPlacement(button, PlacementMode.Top);
        ToolTip.SetShowDelay(button, 400);
        return button;
    }

    private void ShowDelayMenu(Control target)
    {
        ContextMenu menu = new();
        decimal current = Program.DefaultTaskSettings.CaptureSettings.ScreenshotDelay;

        for (int delay = 0; delay <= 5; delay++)
        {
            int selectedDelay = delay;
            MenuItem item = new()
            {
                Header = string.Format(Strings.ScreenshotDelay0S, delay),
                ToggleType = MenuItemToggleType.Radio,
                IsChecked = Math.Abs(current - delay) < 0.01m
            };
            item.Click += (_, _) =>
            {
                Program.DefaultTaskSettings.CaptureSettings.ScreenshotDelay = selectedDelay;
                UpdateCaptureOptions();
                SaveSettings();
            };
            menu.Items.Add(item);
        }

        menu.Open(target);
    }

    private void UpdateCaptureOptions()
    {
        if (_showCursorButton != null)
        {
            _showCursorButton.Classes.Set("checked", Program.DefaultTaskSettings.CaptureSettings.ShowCursor);
        }

        if (_clipboardToggleButton != null && _clipboardToggleIcon != null)
        {
            bool isImage = Program.DefaultTaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.CopyImageToClipboard);
            _clipboardToggleIcon.Text = isImage ? LucideIcons.clipboard_copy : LucideIcons.clipboard_list;
            ToolTip.SetTip(_clipboardToggleButton, isImage
                ? Strings.MainMenuBuilder_CopyImageToClipboard
                : Strings.MainMenuBuilder_CopyFilePathToClipboard);
        }

        if (_delayText != null)
        {
            _delayText.Text = $"{Program.DefaultTaskSettings.CaptureSettings.ScreenshotDelay:0.#}s";
        }
    }

    private async void OnActionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: HotkeyType action })
        {
            return;
        }

        bool restoreTopmost = Program.Settings.ActionsToolbarStayTopMost;
        if (restoreTopmost)
        {
            Topmost = false;
        }

        try
        {
            await TaskHelpers.ExecuteJob(action);
        }
        finally
        {
            if (!_closing)
            {
                Topmost = Program.Settings.ActionsToolbarStayTopMost;
            }
        }
    }

    private ContextMenu CreateToolbarMenu()
    {
        ContextMenu menu = new()
        {
            Cursor = new Cursor(StandardCursorType.Arrow)
        };

        MenuItem close = new() { Header = Strings.ActionsToolbarWindow_Close };
        close.Click += (_, _) => Close();
        menu.Items.Add(close);
        menu.Items.Add(new Separator());

        MenuItem lockPosition = new()
        {
            Header = Strings.ActionsToolbarWindow_LockPosition,
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = Program.Settings.ActionsToolbarLockPosition
        };
        lockPosition.Click += (_, _) =>
        {
            Program.Settings.ActionsToolbarLockPosition = lockPosition.IsChecked;
            UpdateTitleCursor();
            SaveSettings();
        };
        menu.Items.Add(lockPosition);

        MenuItem stayTopmost = new()
        {
            Header = Strings.ActionsToolbarWindow_StayOnTop,
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = Program.Settings.ActionsToolbarStayTopMost
        };
        stayTopmost.Click += (_, _) =>
        {
            Program.Settings.ActionsToolbarStayTopMost = stayTopmost.IsChecked;
            Topmost = stayTopmost.IsChecked;
            SaveSettings();
        };
        menu.Items.Add(stayTopmost);

        MenuItem runAtStartup = new()
        {
            Header = Strings.ActionsToolbarWindow_OpenAtStartup,
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = Program.Settings.ActionsToolbarRunAtStartup
        };
        runAtStartup.Click += (_, _) =>
        {
            Program.Settings.ActionsToolbarRunAtStartup = runAtStartup.IsChecked;
            SaveSettings();
        };
        menu.Items.Add(runAtStartup);
        menu.Items.Add(new Separator());

        MenuItem edit = new() { Header = Strings.ActionsToolbarWindow_Edit };
        edit.Click += async (_, _) => await ShowEditorAsync();
        menu.Items.Add(edit);

        return menu;
    }

    private async System.Threading.Tasks.Task ShowEditorAsync()
    {
        bool restoreTopmost = Program.Settings.ActionsToolbarStayTopMost;
        Topmost = false;

        try
        {
            ActionsToolbarEditorWindow editor = new(RefreshToolbar);
            await editor.ShowDialog(this);
        }
        finally
        {
            if (!_closing)
            {
                Topmost = restoreTopmost;
                RefreshToolbar();
            }
        }
    }

    private void OnTitlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        PointerUpdateKind kind = e.GetCurrentPoint(TitleHandle).Properties.PointerUpdateKind;
        if (kind == PointerUpdateKind.LeftButtonPressed && !Program.Settings.ActionsToolbarLockPosition)
        {
            BeginMoveDrag(e);
            e.Handled = true;
        }
        else if (kind == PointerUpdateKind.MiddleButtonPressed)
        {
            Close();
            e.Handled = true;
        }
    }

    private void UpdateTitleCursor()
    {
        TitleHandle.Cursor = new Cursor(Program.Settings.ActionsToolbarLockPosition
            ? StandardCursorType.Arrow
            : StandardCursorType.SizeAll);
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        RestorePosition();
        _positionReady = true;
        ClampAndSavePosition();
        Activate();
    }

    private void RestorePosition()
    {
        DrawingPoint saved = Program.Settings.ActionsToolbarPosition;
        if (!saved.IsEmpty)
        {
            PixelPoint point = new(saved.X, saved.Y);
            if (Screens.All.Any(screen => screen.WorkingArea.Contains(point)))
            {
                Position = point;
                return;
            }
        }

        DrawingPoint cursor = CaptureHelpers.GetCursorPosition();
        Screen? screen = Screens.ScreenFromPoint(new PixelPoint(cursor.X, cursor.Y)) ?? Screens.Primary;
        if (screen == null)
        {
            return;
        }

        PixelRect area = screen.WorkingArea;
        PixelSize size = PixelSize.FromSize(ClientSize, screen.Scaling);
        Position = new PixelPoint(area.Right - size.Width, area.Bottom - size.Height);
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (_positionReady)
        {
            ClampAndSavePosition();
        }
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_positionReady && e.Property == ClientSizeProperty)
        {
            Dispatcher.UIThread.Post(ClampAndSavePosition, DispatcherPriority.Loaded);
        }
    }

    private void ClampAndSavePosition()
    {
        if (!_positionReady || _adjustingPosition)
        {
            return;
        }

        Screen? screen = Screens.ScreenFromPoint(Position) ?? Screens.Primary;
        if (screen == null)
        {
            return;
        }

        PixelRect area = screen.WorkingArea;
        PixelSize size = PixelSize.FromSize(ClientSize, screen.Scaling);
        int maxX = Math.Max(area.X, area.Right - size.Width);
        int maxY = Math.Max(area.Y, area.Bottom - size.Height);
        PixelPoint adjusted = new(
            Math.Clamp(Position.X, area.X, maxX),
            Math.Clamp(Position.Y, area.Y, maxY));

        if (adjusted != Position)
        {
            _adjustingPosition = true;
            Position = adjusted;
            _adjustingPosition = false;
        }

        Program.Settings.ActionsToolbarPosition = new DrawingPoint(adjusted.X, adjusted.Y);
    }

    private void OnDragEnter(object? sender, DragEventArgs e) => UpdateDragState(e);

    private void OnDragOver(object? sender, DragEventArgs e) => UpdateDragState(e);

    private void UpdateDragState(DragEventArgs e)
    {
        bool supported = e.DataTransfer.TryGetFiles()?.Any() == true ||
            !string.IsNullOrEmpty(e.DataTransfer.TryGetText());
        e.DragEffects = supported ? DragDropEffects.Copy : DragDropEffects.None;
        DropOverlay.IsVisible = supported;
        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        DropOverlay.IsVisible = false;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        DropOverlay.IsVisible = false;

        FormsDataObject dataObject = new();
        string[] files = e.DataTransfer.TryGetFiles()?
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrEmpty(path))
            .Cast<string>()
            .ToArray() ?? [];

        if (files.Length > 0)
        {
            dataObject.SetData(FormsDataFormats.FileDrop, files);
        }

        string? text = e.DataTransfer.TryGetText();
        if (!string.IsNullOrEmpty(text))
        {
            dataObject.SetText(text);
        }

        UploadManager.DragDropUpload(dataObject);
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private static void SaveSettings() => SettingManager.SaveApplicationConfigAsync();
}
