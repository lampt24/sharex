using ShareX.HelpersLib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace ShareX.ScreenCaptureLib;

internal sealed class ScrollingCaptureTarget
{
    private readonly ScrollPattern pattern;
    private readonly ScrollingCaptureDirection direction;
    public Point Point { get; }
    public IntPtr Handle { get; }
    public IntPtr RootHandle => GetAncestor(Handle, 2);
    public Rectangle Bounds { get; }
    public bool SupportsAutomation => pattern != null;

    private ScrollingCaptureTarget(Point point, IntPtr handle, Rectangle bounds,
        ScrollingCaptureDirection direction, ScrollPattern pattern)
    {
        Point = point;
        Handle = handle;
        Bounds = bounds;
        this.direction = direction;
        this.pattern = pattern;
    }

    public static ScrollingCaptureTarget ResolveAtPoint(Point point, ScrollingCaptureDirection direction, int parentIndex = 0)
    {
        IntPtr handle = WindowFromPoint(point);
        if (handle == IntPtr.Zero) return null;
        Rectangle bounds = Rectangle.Intersect(new WindowInfo(GetAncestor(handle, 2)).Rectangle, MonitorBounds(point));
        if (bounds.Width < 2 || bounds.Height < 2) return null;
        return Resolve(point, bounds, direction, parentIndex, true);
    }

    // Maximized windows extend past their monitor; with a monitor beside it the
    // frame would straddle both monitors and no longer match the panel.
    private static Rectangle MonitorBounds(Point point) => System.Windows.Forms.Screen.FromPoint(point).Bounds;

    public static ScrollingCaptureTarget ResolveNativeAtPoint(Point point, ScrollingCaptureDirection direction)
    {
        IntPtr handle = WindowFromPoint(point);
        if (handle == IntPtr.Zero) return null;
        Rectangle bounds = Rectangle.Intersect(new WindowInfo(handle).Rectangle, MonitorBounds(point));
        if (bounds.Width < 2 || bounds.Height < 2 || !bounds.Contains(point)) return null;
        return new ScrollingCaptureTarget(point, handle, bounds, direction, null);
    }

    public ScrollingCaptureTarget AtPoint(Point point) => new(point, WindowFromPoint(point), Bounds, direction, pattern);

    public static ScrollingCaptureTarget Resolve(Point point, Rectangle captureBounds, ScrollingCaptureDirection direction,
        int parentIndex = 0, bool useNativeBounds = false)
    {
        IntPtr handle = WindowFromPoint(point);
        IntPtr root = GetAncestor(handle, 2);
        // Levels are ordered innermost first. Scroll containers are preferred since they
        // scroll precisely, but Chromium/Electron and many custom UIs expose no ScrollPattern.
        // Their UI panels still frame what is under the pointer instead of the whole window.
        var scrollLevels = new List<ScrollingCaptureTarget>();
        var panelLevels = new List<ScrollingCaptureTarget>();
        try
        {
            AutomationElement element = AutomationElement.FromPoint(new System.Windows.Point(point.X, point.Y));
            for (int depth = 0; element != null && depth < 64; depth++)
            {
                AutomationElement.AutomationElementInformation current = element.Current;
                int nativeHandle = current.NativeWindowHandle;
                if (nativeHandle != 0 && GetAncestor(new IntPtr(nativeHandle), 2) != root) break;
                var bounds = current.BoundingRectangle;
                if (!bounds.IsEmpty)
                {
                    Rectangle panel = Rectangle.FromLTRB((int)Math.Ceiling(bounds.Left), (int)Math.Ceiling(bounds.Top),
                        (int)Math.Floor(bounds.Right), (int)Math.Floor(bounds.Bottom));
                    Rectangle clipped = Rectangle.Intersect(captureBounds, panel);
                    if (clipped.Width > 1 && clipped.Height > 1 && clipped.Contains(point))
                    {
                        if (element.TryGetCurrentPattern(ScrollPattern.Pattern, out object value) && value is ScrollPattern scroll &&
                            (direction == ScrollingCaptureDirection.Horizontal ? scroll.Current.HorizontallyScrollable : scroll.Current.VerticallyScrollable))
                            AddLevel(scrollLevels, new ScrollingCaptureTarget(point, handle, clipped, direction, scroll));
                        else if (IsPanel(current.ControlType, clipped))
                            AddLevel(panelLevels, new ScrollingCaptureTarget(point, handle, clipped, direction, null));
                    }
                }
                if (nativeHandle != 0 && new IntPtr(nativeHandle) == root) break;
                element = TreeWalker.ControlViewWalker.GetParent(element);
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex);
        }
        List<ScrollingCaptureTarget> levels = scrollLevels.Count > 0 ? scrollLevels : panelLevels;
        if (levels.Count > 0) return levels[Math.Min(parentIndex, levels.Count - 1)];
        if (useNativeBounds) captureBounds = Rectangle.Intersect(captureBounds, new WindowInfo(handle).Rectangle);
        return new ScrollingCaptureTarget(point, handle, captureBounds, direction, null);
    }

    private const int MinPanelSize = 64;

    // Containers only: items, text, buttons and toolbars are too small or too specific to capture.
    private static bool IsPanel(ControlType type, Rectangle bounds) =>
        bounds.Width >= MinPanelSize && bounds.Height >= MinPanelSize &&
        (type == ControlType.Pane || type == ControlType.Group || type == ControlType.Document ||
         type == ControlType.List || type == ControlType.Tree || type == ControlType.DataGrid ||
         type == ControlType.Table || type == ControlType.Custom || type == ControlType.Edit);

    // Nested wrappers often share bounds; Tab should always move to a visibly larger panel.
    private static void AddLevel(List<ScrollingCaptureTarget> levels, ScrollingCaptureTarget target)
    {
        if (levels.Count == 0 || levels[^1].Bounds != target.Bounds) levels.Add(target);
    }

    // Once resolved, never switch to another panel when this one reaches its end.
    public void Scroll(int amount)
    {
        for (int i = 0; i < Math.Max(1, amount); i++)
            pattern.Scroll(direction == ScrollingCaptureDirection.Horizontal ? ScrollAmount.SmallIncrement : ScrollAmount.NoAmount,
                direction == ScrollingCaptureDirection.Vertical ? ScrollAmount.SmallIncrement : ScrollAmount.NoAmount);
    }

    public void ScrollToStart() => pattern.SetScrollPercent(
        direction == ScrollingCaptureDirection.Horizontal ? 0 : ScrollPattern.NoScroll,
        direction == ScrollingCaptureDirection.Vertical ? 0 : ScrollPattern.NoScroll);

    public void PositionPointer()
    {
        if (GetAncestor(WindowFromPoint(Point), 2) != GetAncestor(Handle, 2))
            throw new InvalidOperationException(ScrollingCapturePointWindow.Text("ScrollingCaptureWindow_Target_obscured"));
        NativeMethods.SetCursorPos(Point.X, Point.Y);
    }

    public void SendKey(VirtualKeyCode key)
    {
        SendMessage(0x0100, (int)key, 0);
        SendMessage(0x0101, (int)key, unchecked((int)0xC0000000));
    }

    public void SendMessage(int message, int parameter, int data = 0)
    {
        if (NativeMethods.SendMessageTimeout(Handle, message, parameter, data,
            SendMessageTimeoutFlags.SMTO_ABORTIFHUNG, 1000, out IntPtr _) == IntPtr.Zero)
            throw new InvalidOperationException(ScrollingCapturePointWindow.Text("ScrollingCaptureWindow_No_scroll_movement"));
    }

    public void ScrollHorizontalWheel()
    {
        int coordinates = (Point.X & 0xffff) | ((Point.Y & 0xffff) << 16);
        SendMessage((int)WindowsMessages.MOUSEHWHEEL, 120 << 16, coordinates);
    }

    public void ScrollVerticalWheel(int amount)
    {
        int coordinates = (Point.X & 0xffff) | ((Point.Y & 0xffff) << 16);
        for (int i = 0; i < Math.Max(1, amount); i++)
            SendMessage((int)WindowsMessages.MOUSEWHEEL, unchecked(-120 << 16), coordinates);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr handle, uint flags);
}
