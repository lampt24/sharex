#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

using ShareX.HelpersLib;
using System;
using System.Drawing;
using System.Windows.Automation;

namespace ShareX.ScreenCaptureLib;

internal static class ExcelHorizontalScroller
{
    public static bool ScrollColumnRight(IntPtr windowHandle, Point scrollPoint)
    {
        try
        {
            IntPtr rootHandle = GetRootHandle(windowHandle);
            IntPtr scrollBarHandle = FindHorizontalScrollBar(rootHandle, scrollPoint);

            if (scrollBarHandle == IntPtr.Zero)
            {
                return false;
            }

            AutomationElement scrollBar = AutomationElement.FromHandle(scrollBarHandle);
            PropertyCondition nameCondition = new(AutomationElement.NameProperty, "Column right");
            AutomationElement button = scrollBar.FindFirst(TreeScope.Descendants, nameCondition);

            if (button?.TryGetCurrentPattern(InvokePattern.Pattern, out object pattern) == true &&
                pattern is InvokePattern invokePattern)
            {
                invokePattern.Invoke();
                return true;
            }
        }
        catch (Exception exception)
        {
            DebugHelper.WriteException(exception);
        }

        return false;
    }

    private static IntPtr GetRootHandle(IntPtr handle)
    {
        IntPtr parent;

        while ((parent = NativeMethods.GetParent(handle)) != IntPtr.Zero)
        {
            handle = parent;
        }

        return handle;
    }

    private static IntPtr FindHorizontalScrollBar(IntPtr rootHandle, Point scrollPoint)
    {
        IntPtr result = IntPtr.Zero;
        int nearestDistance = int.MaxValue;

        NativeMethods.EnumChildWindows(rootHandle, (handle, _) =>
        {
            if (NativeMethods.GetClassName(handle).Equals("NUIScrollbar", StringComparison.OrdinalIgnoreCase) &&
                NativeMethods.GetWindowText(handle).Equals("Horizontal", StringComparison.OrdinalIgnoreCase))
            {
                WindowInfo info = new(handle);
                Rectangle bounds = info.Rectangle;
                // Split views can expose several horizontal scrollbars. Use the
                // nearest one below the selected content, in the same column.
                if (NativeMethods.IsWindowVisible(handle) && bounds.Left <= scrollPoint.X &&
                    bounds.Right > scrollPoint.X && bounds.Top >= scrollPoint.Y)
                {
                    int distance = bounds.Top - scrollPoint.Y;
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        result = handle;
                    }
                }
            }

            return true;
        }, IntPtr.Zero);

        return result;
    }
}
