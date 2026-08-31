#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

using ShareX.HelpersLib;
using System;
using System.Windows.Automation;

namespace ShareX.ScreenCaptureLib;

internal static class ExcelHorizontalScroller
{
    public static bool ScrollColumnRight(IntPtr windowHandle)
    {
        try
        {
            IntPtr rootHandle = GetRootHandle(windowHandle);
            IntPtr scrollBarHandle = FindHorizontalScrollBar(rootHandle);

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

    private static IntPtr FindHorizontalScrollBar(IntPtr rootHandle)
    {
        IntPtr result = IntPtr.Zero;

        NativeMethods.EnumChildWindows(rootHandle, (handle, _) =>
        {
            if (NativeMethods.GetClassName(handle).Equals("NUIScrollbar", StringComparison.OrdinalIgnoreCase) &&
                NativeMethods.GetWindowText(handle).Equals("Horizontal", StringComparison.OrdinalIgnoreCase))
            {
                result = handle;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return result;
    }
}
