using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ShareX.HelpersLib;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;

namespace ShareX.ScreenCaptureLib;

// Only the frame and instruction label are windows. The application underneath
// remains live, and hit testing / UI Automation sees its real controls.
internal sealed class ScrollingCapturePointWindow : Avalonia.Controls.Window
{
    public static async Task<ScrollingCaptureTarget> SelectAsync(ScrollingCaptureDirection direction)
    {
        var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        DrawingPoint? clicked = null;
        int parentIndex = 0;
        bool mouseDown = false;
        ScrollingCaptureRegionWindow frame = null;
        DrawingRectangle framedBounds = DrawingRectangle.Empty;
        var hint = new ScrollingCapturePointWindow
        {
            Title = Text("ScrollingCaptureWindow_Select_scroll_point"),
            WindowDecorations = WindowDecorations.None,
            CanResize = false, ShowInTaskbar = false, ShowActivated = false,
            Topmost = true, Width = 540, SizeToContent = SizeToContent.Height,
            Content = new Border
            {
                Background = Brushes.Black, Padding = new Thickness(12),
                Child = new TextBlock
                {
                    Text = Text("ScrollingCaptureWindow_Select_scroll_point"),
                    Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap
                }
            }
        };
        var screen = Screen.FromPoint(CaptureHelpers.GetCursorPosition()).WorkingArea;
        hint.Position = new PixelPoint(screen.Left + 20, screen.Top + 20);
        hint.Closed += (_, _) => cancelled.TrySetResult(true);
        using var keyboard = new KeyboardHook();
        keyboard.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) cancelled.TrySetResult(true);
            else if (e.KeyCode == Keys.Enter) clicked = CaptureHelpers.GetCursorPosition();
            else if (e.KeyCode == Keys.Tab) parentIndex = Math.Max(0, parentIndex + (e.Shift ? -1 : 1));
            else return;
            e.Handled = e.SuppressKeyPress = true;
        };
        keyboard.KeyUp += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab)
                e.Handled = e.SuppressKeyPress = true;
        };
        HookProc mouseCallback = (code, message, data) =>
        {
            if (code >= 0)
            {
                int msg = message.ToInt32();
                if (msg == 0x0201 || msg == 0x0204) // swallow selection/cancel down
                {
                    mouseDown = true;
                    return new IntPtr(1);
                }
                if (mouseDown && (msg == 0x0202 || msg == 0x0205))
                {
                    mouseDown = false;
                    if (msg == 0x0205) cancelled.TrySetResult(true);
                    else clicked = Marshal.PtrToStructure<DrawingPoint>(data);
                    return new IntPtr(1);
                }
            }
            return NativeMethods.CallNextHookEx(IntPtr.Zero, code, message, data);
        };
        using Process process = Process.GetCurrentProcess();
        IntPtr hook = NativeMethods.SetWindowsHookEx(14, mouseCallback,
            NativeMethods.GetModuleHandle(process.MainModule.ModuleName), 0);
        if (hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            hint.Show();
            // Exclude our overlays, not every window in the ShareX process.
            // Otherwise ShareX's own scroll panels can never be selected.
            bool IsOverlay(IntPtr root) => root == hint.TryGetPlatformHandle()?.Handle ||
                root == frame?.TryGetPlatformHandle()?.Handle;
            var hover = new ScrollingCaptureHoverSession(point =>
            {
                ScrollingCaptureTarget native = ScrollingCaptureTarget.ResolveNativeAtPoint(point, direction);
                return native == null || IsOverlay(native.RootHandle) ? null : native;
            }, (point, parent) => Task.Run(() => ScrollingCaptureTarget.ResolveAtPoint(point, direction, parent)));
            DrawingPoint previousPoint = CaptureHelpers.GetCursorPosition();
            while (!cancelled.Task.IsCompleted)
            {
                DrawingPoint point = clicked ?? CaptureHelpers.GetCursorPosition();
                if (point != previousPoint) parentIndex = 0;
                previousPoint = point;
                int requestedParent = parentIndex;
                bool selecting = clicked.HasValue;
                // Native bounds are immediate. UIA refines them when ready;
                // neither hover feedback nor clicking waits on a provider.
                ScrollingCaptureTarget target = hover.Update(point, requestedParent);
                if (selecting)
                {
                    // The preview may still be pending for this point; resolve it directly
                    // so a click selects the panel rather than the whole window.
                    ScrollingCaptureTarget precise = await ResolveSelectionAsync(point, direction, requestedParent);
                    if (precise != null && !IsOverlay(precise.RootHandle)) target = precise;
                }
                bool valid = target != null && target.Bounds.Width > 1 && target.Bounds.Height > 1;
                if (selecting)
                {
                    if (valid) return target;
                    clicked = null;
                }
                DrawingRectangle bounds = valid ? target.Bounds : DrawingRectangle.Empty;
                if (bounds != framedBounds)
                {
                    frame?.Close();
                    frame = null;
                    framedBounds = bounds;
                    if (!bounds.IsEmpty)
                    {
                        frame = new ScrollingCaptureRegionWindow(bounds);
                        frame.Show();
                    }
                }
                await Task.WhenAny(Task.Delay(100), cancelled.Task);
            }
            return null;
        }
        finally
        {
            NativeMethods.UnhookWindowsHookEx(hook);
            GC.KeepAlive(mouseCallback);
            frame?.Close();
            hint.Close();
        }
    }

    private static async Task<ScrollingCaptureTarget> ResolveSelectionAsync(DrawingPoint point,
        ScrollingCaptureDirection direction, int parentIndex)
    {
        Task<ScrollingCaptureTarget> resolve = Task.Run(() => ScrollingCaptureTarget.ResolveAtPoint(point, direction, parentIndex));
        await Task.WhenAny(resolve, Task.Delay(2000));
        if (resolve.Status == TaskStatus.RanToCompletion) return resolve.Result;
        // A slow or failing provider falls back to the hover preview.
        _ = resolve.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        return null;
    }

    internal static string Text(string key) => Localization.Strings.ResourceManager.GetString(key) ?? key;
}
