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

#nullable enable

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform;
using Avalonia.Threading;
using ShareX.AvaloniaUI.Controls;
using ShareX.AvaloniaUI.Theming;
using ShareX.HelpersLib;
using ShareX.Icons;
using ShareX.Localization;
using System;
using System.Linq;
using DrawingPoint = System.Drawing.Point;

namespace ShareX;

public partial class ActionsToolbarWindow : Window
{
    private Button? _showCursorButton;
    private Button? _clipboardToggleButton;
    private TextBlock? _clipboardToggleText;
    private TextBlock? _delayText;
    private bool _positionReady;
    private bool _adjustingPosition;
    private bool _closing;

    public ActionsToolbarWindow()
    {
        InitializeComponent();
        RequestedThemeVariant = ThemeManager.GetCurrentTheme();
        Topmost = ApplicationState.Settings.ActionsToolbarStayTopMost;
        ApplicationState.Settings.ActionsToolbarList ??= [];
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

        if (ApplicationState.Settings.ActionsToolbarList.SequenceEqual(legacyDefault))
        {
            ApplicationState.Settings.ActionsToolbarList =
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

        int scrollingCaptureIndex = ApplicationState.Settings.ActionsToolbarList.IndexOf(HotkeyType.ScrollingCapture);
        if (scrollingCaptureIndex >= 0 &&
            !ApplicationState.Settings.ActionsToolbarList.Contains(HotkeyType.HorizontalScrollingCapture))
        {
            ApplicationState.Settings.ActionsToolbarList.Insert(scrollingCaptureIndex + 1, HotkeyType.HorizontalScrollingCapture);
        }
    }

    internal void RefreshToolbar()
    {
        while (ToolbarItems.Children.Count > 1)
        {
            ToolbarItems.Children.RemoveAt(1);
        }

        foreach (HotkeyType action in ApplicationState.Settings.ActionsToolbarList)
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

            IconPresenter icon = new()
            {
                Icon = TaskHelpers.FindMenuIcon(action),
                FontSize = 17,
                Foreground = this.FindResource("ShareX.Brush.Accent") as Avalonia.Media.IBrush,
                IsHitTestVisible = false
            };

            Button button = new()
            {
                Content = icon,
                Tag = action
            };
            button.Classes.Add("toolbar-action");
            ToolTip.SetTip(button, ActionsToolbarTooltipBuilder.Build(
                action,
                action.GetLocalizedDescription(),
                ApplicationState.HotkeysConfigOrNull?.Hotkeys));
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

        _showCursorButton = CreateCaptureOptionButton(AppIcons.Pointer, Strings.MainMenuBuilder_ShowCursor);
        _showCursorButton.Click += (_, _) =>
        {
            ApplicationState.DefaultTaskSettings.CaptureSettings.ShowCursor =
                !ApplicationState.DefaultTaskSettings.CaptureSettings.ShowCursor;
            UpdateCaptureOptions();
            SaveSettings();
        };
        ToolbarItems.Children.Add(_showCursorButton);

        _clipboardToggleText = new TextBlock
        {
            FontSize = 11,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = Avalonia.Media.TextAlignment.Center,
            Foreground = this.FindResource("ShareX.Brush.Accent") as Avalonia.Media.IBrush,
            IsHitTestVisible = false
        };
        _clipboardToggleButton = new Button { Content = _clipboardToggleText, Width = 48, MinWidth = 48 };
        _clipboardToggleButton.Classes.Add("toolbar-action");
        ToolTip.SetPlacement(_clipboardToggleButton, PlacementMode.Top);
        ToolTip.SetShowDelay(_clipboardToggleButton, 400);
        _clipboardToggleButton.Click += (_, _) =>
        {
            TaskHelpers.ToggleClipboardCopyMode();
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
            ApplicationState.DefaultTaskSettings.CaptureSettings.ScreenshotDelay.ToString("0.#")));
        delayButton.Click += (_, _) => ShowDelayMenu(delayButton);
        ToolbarItems.Children.Add(delayButton);

        UpdateCaptureOptions();
    }

    private Button CreateCaptureOptionButton(string iconKey, string tooltip)
    {
        IconPresenter icon = new()
        {
            Icon = iconKey,
            FontSize = 17,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = this.FindResource("ShareX.Brush.Accent") as Avalonia.Media.IBrush,
            IsHitTestVisible = false
        };

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
        decimal current = ApplicationState.DefaultTaskSettings.CaptureSettings.ScreenshotDelay;

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
                ApplicationState.DefaultTaskSettings.CaptureSettings.ScreenshotDelay = selectedDelay;
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
            _showCursorButton.Classes.Set("checked", ApplicationState.DefaultTaskSettings.CaptureSettings.ShowCursor);
        }

        if (_clipboardToggleButton != null && _clipboardToggleText != null)
        {
            bool isImage = ApplicationState.DefaultTaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.CopyImageToClipboard);
            _clipboardToggleText.Text = isImage ? "IMAGE" : "PATH";
            ToolTip.SetTip(_clipboardToggleButton, isImage
                ? Strings.MainMenuBuilder_CopyImageToClipboard
                : Strings.MainMenuBuilder_CopyFilePathToClipboard);
        }

        if (_delayText != null)
        {
            _delayText.Text = $"{ApplicationState.DefaultTaskSettings.CaptureSettings.ScreenshotDelay:0.#}s";
        }
    }

    internal void RefreshClipboardCopyMode()
    {
        UpdateCaptureOptions();
    }

    private async void OnActionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: HotkeyType action })
        {
            return;
        }

        bool restoreTopmost = ApplicationState.Settings.ActionsToolbarStayTopMost;
        if (restoreTopmost)
        {
            Topmost = false;
        }

        try
        {
            await ActionsToolbarActionRunner.RunAsync(
                Hide,
                () =>
                {
                    if (!_closing)
                    {
                        Show();
                    }
                },
                () => TaskHelpers.ExecuteJob(action));
        }
        finally
        {
            if (!_closing)
            {
                Topmost = ApplicationState.Settings.ActionsToolbarStayTopMost;
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
            IsChecked = ApplicationState.Settings.ActionsToolbarLockPosition
        };
        lockPosition.Click += (_, _) =>
        {
            ApplicationState.Settings.ActionsToolbarLockPosition = lockPosition.IsChecked;
            UpdateTitleCursor();
            SaveSettings();
        };
        menu.Items.Add(lockPosition);

        MenuItem stayTopmost = new()
        {
            Header = Strings.ActionsToolbarWindow_StayOnTop,
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = ApplicationState.Settings.ActionsToolbarStayTopMost
        };
        stayTopmost.Click += (_, _) =>
        {
            ApplicationState.Settings.ActionsToolbarStayTopMost = stayTopmost.IsChecked;
            Topmost = stayTopmost.IsChecked;
            SaveSettings();
        };
        menu.Items.Add(stayTopmost);

        MenuItem runAtStartup = new()
        {
            Header = Strings.ActionsToolbarWindow_OpenAtStartup,
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = ApplicationState.Settings.ActionsToolbarRunAtStartup
        };
        runAtStartup.Click += (_, _) =>
        {
            ApplicationState.Settings.ActionsToolbarRunAtStartup = runAtStartup.IsChecked;
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
        bool restoreTopmost = ApplicationState.Settings.ActionsToolbarStayTopMost;
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
        if (kind == PointerUpdateKind.LeftButtonPressed && !ApplicationState.Settings.ActionsToolbarLockPosition)
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
        TitleHandle.Cursor = new Cursor(ApplicationState.Settings.ActionsToolbarLockPosition
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
        DrawingPoint saved = ApplicationState.Settings.ActionsToolbarPosition;
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

        ApplicationState.Settings.ActionsToolbarPosition = new DrawingPoint(adjusted.X, adjusted.Y);
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

        UploadManager.DragDropUpload(e.DataTransfer);
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private static void SaveSettings() => SettingManager.SaveApplicationConfigAsync();
}
