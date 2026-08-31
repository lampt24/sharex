#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

using System;

namespace ShareX.ScreenCaptureLib;

public static class ScrollingCaptureDelay
{
    public static int GetPostScrollDelay(int scrollDelay, int processingElapsed, ScrollingCaptureDirection direction)
    {
        return direction == ScrollingCaptureDirection.Horizontal
            ? Math.Max(0, scrollDelay)
            : Math.Max(0, scrollDelay - processingElapsed);
    }
}
