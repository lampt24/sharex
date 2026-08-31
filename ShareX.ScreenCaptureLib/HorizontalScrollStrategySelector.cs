#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

#nullable enable

using System;

namespace ShareX.ScreenCaptureLib;

public enum HorizontalScrollStrategy
{
    ScrollMessage,
    ExcelScrollBarButton
}

public static class HorizontalScrollStrategySelector
{
    public static HorizontalScrollStrategy Select(string? processName)
    {
        return string.Equals(processName, "EXCEL", StringComparison.OrdinalIgnoreCase)
            ? HorizontalScrollStrategy.ExcelScrollBarButton
            : HorizontalScrollStrategy.ScrollMessage;
    }
}
