#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

#nullable enable

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ShareX.ScreenCaptureLib;

public static class ScrollingCaptureImageCombiner
{
    // Scrolled content is rarely pixel-identical: fractional DPI offsets re-raster text,
    // and hover effects or scrollbar thumbs change too. Pixels are compared with tolerance.
    private const int ChannelTolerance = 32;
    // Only pixels that changed between frames are scored, so static sidebars, toolbars and
    // blank background never make a scrolled frame look unmoved.
    private const double MaxMismatchRatio = 0.3;
    // Slightly prefer smaller shifts so repeated content does not match at a distant offset.
    private const double ShiftPenalty = 0.02;
    private const int SampledRows = 48;

    /// <param name="previousFrame">The frame captured before <paramref name="currentImage"/>;
    /// when omitted, <paramref name="result"/> must be that frame.</param>
    public static Bitmap? Combine(Bitmap result, Bitmap currentImage, ScrollingCaptureDirection direction,
        Bitmap? previousFrame = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(currentImage);

        return direction switch
        {
            ScrollingCaptureDirection.Horizontal => CombineHorizontal(result, previousFrame ?? result, currentImage, out _),
            _ => throw new NotSupportedException("Vertical capture continues to use the legacy combiner.")
        };
    }

    /// <param name="movingBounds">The part of the frame that changed, in frame coordinates.</param>
    internal static Bitmap? CombineHorizontal(Bitmap result, Bitmap previousFrame, Bitmap currentImage, out Rectangle movingBounds)
    {
        movingBounds = Rectangle.Empty;

        if (result.Height != currentImage.Height || result.Width < currentImage.Width)
        {
            return null;
        }

        (int shift, Rectangle moving) = Match(previousFrame, currentImage);

        if (shift <= 0)
        {
            return null;
        }

        movingBounds = moving;
        int width = currentImage.Width;
        int rightEdge = width - moving.Right;
        Bitmap combined = new(result.Width + shift, result.Height);

        using (Graphics graphics = Graphics.FromImage(combined))
        {
            graphics.DrawImage(result,
                new Rectangle(0, 0, result.Width - rightEdge, result.Height),
                new Rectangle(0, 0, result.Width - rightEdge, result.Height),
                GraphicsUnit.Pixel);
            graphics.DrawImage(currentImage,
                new Rectangle(result.Width - rightEdge, 0, shift, currentImage.Height),
                new Rectangle(width - rightEdge - shift, 0, shift, currentImage.Height),
                GraphicsUnit.Pixel);
            if (rightEdge > 0)
            {
                graphics.DrawImage(currentImage,
                    new Rectangle(result.Width - rightEdge + shift, 0, rightEdge, currentImage.Height),
                    new Rectangle(width - rightEdge, 0, rightEdge, currentImage.Height),
                    GraphicsUnit.Pixel);
            }
        }

        return combined;
    }

    /// <summary>
    /// Returns how far the content moved left between two frames of the same size:
    /// 0 when (almost) nothing changed, or -1 when the change is not a horizontal scroll.
    /// </summary>
    internal static int FindHorizontalShift(Bitmap previousFrame, Bitmap currentImage)
    {
        ArgumentNullException.ThrowIfNull(previousFrame);
        ArgumentNullException.ThrowIfNull(currentImage);
        return Match(previousFrame, currentImage).Shift;
    }

    private static (int Shift, Rectangle Moving) Match(Bitmap previousFrame, Bitmap currentImage)
    {
        if (previousFrame.Size != currentImage.Size || currentImage.Width < 2)
        {
            return (-1, Rectangle.Empty);
        }

        int width = currentImage.Width;
        int height = currentImage.Height;
        int[] previous = ReadPixels(previousFrame);
        int[] current = ReadPixels(currentImage);
        bool[] changedColumns = new bool[width];
        bool[] changedRows = new bool[height];
        long changedPixels = 0;
        int changedRowCount = 0;

        for (int y = 0; y < height; y++)
        {
            int offset = y * width;

            for (int x = 0; x < width; x++)
            {
                if (!IsSimilar(previous[offset + x], current[offset + x]))
                {
                    changedPixels++;
                    changedColumns[x] = true;
                    if (!changedRows[y])
                    {
                        changedRows[y] = true;
                        changedRowCount++;
                    }
                }
            }
        }

        if (changedPixels == 0 || changedPixels * 1000 <= (long)width * height)
        {
            return (0, Rectangle.Empty);
        }

        // Columns that never change (sidebars, a vertical scrollbar) stay outside the match.
        int leftEdge = Array.IndexOf(changedColumns, true);
        int rightEdge = width - 1 - Array.LastIndexOf(changedColumns, true);
        int contentRight = width - rightEdge;
        int[] rows = GetSampleRows(changedRows, changedRowCount);
        int[][] changedPrefix = new int[rows.Length][];

        for (int i = 0; i < rows.Length; i++)
        {
            int offset = rows[i] * width;
            int[] prefix = changedPrefix[i] = new int[width + 1];

            for (int x = 0; x < width; x++)
            {
                prefix[x + 1] = prefix[x] + (IsSimilar(previous[offset + x], current[offset + x]) ? 0 : 1);
            }
        }

        long minInformative = Math.Max(1, CountInformative(changedPrefix, leftEdge, contentRight - 1) / 4);
        int contentWidth = contentRight - leftEdge;
        int bestShift = -1;
        double bestScore = double.MaxValue;

        for (int shift = 1; shift < contentWidth; shift++)
        {
            double penalty = ShiftPenalty * shift / contentWidth;
            double limitRatio = Math.Min(MaxMismatchRatio, bestScore - penalty);
            long informative = CountInformative(changedPrefix, leftEdge, contentRight - shift);

            if (limitRatio < 0 || informative < minInformative)
            {
                break;
            }

            long allowed = (long)(informative * limitRatio);
            long mismatches = 0;

            for (int i = 0; i < rows.Length && mismatches <= allowed; i++)
            {
                int offset = rows[i] * width;

                for (int x = leftEdge; x < contentRight - shift; x++)
                {
                    int currentPixel = current[offset + x];

                    if (!IsSimilar(previous[offset + x], currentPixel) &&
                        !IsSimilar(previous[offset + x + shift], currentPixel) &&
                        ++mismatches > allowed)
                    {
                        break;
                    }
                }
            }

            if (mismatches > allowed)
            {
                continue;
            }

            double score = (double)mismatches / informative + penalty;

            if (score < bestScore)
            {
                bestScore = score;
                bestShift = shift;
            }
        }

        int top = Array.IndexOf(changedRows, true);
        int bottom = Array.LastIndexOf(changedRows, true) + 1;

        if (bestShift > 0)
        {
            (top, bottom) = FindScrolledRows(previous, current, width, leftEdge, contentRight, bestShift, top, bottom);
        }

        return (bestShift, Rectangle.FromLTRB(leftEdge, top, contentRight, bottom));
    }

    // Rows whose changes follow the shift. A scrollbar thumb moves the opposite way and a
    // hover highlight does not move with the content; neither belongs to the scrolled area.
    private static (int Top, int Bottom) FindScrolledRows(int[] previous, int[] current, int width,
        int left, int right, int shift, int changedTop, int changedBottom)
    {
        const int MaxBackgroundRows = 24;
        int top = -1;
        int bottom = -1;

        for (int y = changedTop; y < changedBottom; y++)
        {
            int offset = y * width;
            int changed = 0;
            int mismatches = 0;

            for (int x = left; x < right - shift; x++)
            {
                int currentPixel = current[offset + x];

                if (!IsSimilar(previous[offset + x], currentPixel))
                {
                    changed++;
                    if (!IsSimilar(previous[offset + x + shift], currentPixel)) mismatches++;
                }
            }

            if (changed > 0 && mismatches <= changed * MaxMismatchRatio)
            {
                if (top < 0) top = y;
                bottom = y + 1;
            }
        }

        if (top < 0)
        {
            return (changedTop, changedBottom);
        }

        // The background above the first text row (e.g. a column header's padding) never
        // changes; include it up to a separator line so headers are not clipped.
        int background = current[top * width + left];

        for (int rows = 0; rows < MaxBackgroundRows && top > 0 && IsUniformRow(current, width, top - 1, left, right, background); rows++)
        {
            top--;
        }

        return (top, bottom);
    }

    /// <summary>
    /// Rebuilds rows that did not scroll (title bar, toolbars, empty space, status bar) as if the
    /// window were as wide as the stitched result: left-anchored content stays, right-anchored
    /// content moves to the new right edge and the blank space between them is widened.
    /// Stitched strips would otherwise repeat those rows' buttons and labels.
    /// </summary>
    internal static Bitmap? ExtendStaticRows(Bitmap result, Bitmap firstFrame, int bandTop, int bandBottom)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(firstFrame);

        int frameWidth = firstFrame.Width;
        int extra = result.Width - frameWidth;

        if (extra <= 0 || firstFrame.Height != result.Height || bandTop < 0 || bandBottom > result.Height || bandTop >= bandBottom)
        {
            return null;
        }

        int split = FindStaticSplitColumn(ReadPixels(firstFrame), frameWidth, firstFrame.Height, bandTop, bandBottom);

        if (split < 1)
        {
            return null;
        }

        Bitmap output = new(result.Width, result.Height);

        using (Graphics graphics = Graphics.FromImage(output))
        {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.DrawImage(result, new Rectangle(0, 0, result.Width, result.Height),
                new Rectangle(0, 0, result.Width, result.Height), GraphicsUnit.Pixel);

            foreach ((int top, int bottom) in new[] { (0, bandTop), (bandBottom, result.Height) })
            {
                int height = bottom - top;

                if (height <= 0)
                {
                    continue;
                }

                graphics.DrawImage(firstFrame, new Rectangle(0, top, split, height),
                    new Rectangle(0, top, split, height), GraphicsUnit.Pixel);
                graphics.DrawImage(firstFrame, new Rectangle(split, top, extra, height),
                    new Rectangle(split - 1, top, 1, height), GraphicsUnit.Pixel);
                graphics.DrawImage(firstFrame, new Rectangle(split + extra, top, frameWidth - split, height),
                    new Rectangle(split, top, frameWidth - split, height), GraphicsUnit.Pixel);
            }
        }

        return output;
    }

    // Splits through the middle of the longest run of columns where every static row is blank,
    // so no icon or label is cut in half.
    private static int FindStaticSplitColumn(int[] pixels, int width, int height, int bandTop, int bandBottom)
    {
        int bestStart = -1;
        int bestLength = 0;
        int runStart = -1;

        for (int x = 1; x < width; x++)
        {
            bool blank = true;

            for (int y = 0; y < height && blank; y++)
            {
                if (y == bandTop)
                {
                    y = bandBottom - 1;
                    continue;
                }

                int offset = y * width;
                blank = IsStrictlySimilar(pixels[offset + x], pixels[offset + x - 1]);
            }

            if (!blank)
            {
                runStart = -1;
                continue;
            }

            if (runStart < 0) runStart = x;

            if (x - runStart + 1 > bestLength)
            {
                bestStart = runStart;
                bestLength = x - runStart + 1;
            }
        }

        return bestLength == 0 ? -1 : bestStart + bestLength / 2;
    }

    private static bool IsStrictlySimilar(int a, int b)
    {
        const int StrictTolerance = 4;

        return Math.Abs(((a >> 16) & 0xFF) - ((b >> 16) & 0xFF)) <= StrictTolerance &&
            Math.Abs(((a >> 8) & 0xFF) - ((b >> 8) & 0xFF)) <= StrictTolerance &&
            Math.Abs((a & 0xFF) - (b & 0xFF)) <= StrictTolerance;
    }

    private static bool IsUniformRow(int[] pixels, int width, int y, int left, int right, int color)
    {
        const int StrictTolerance = 4;
        int offset = y * width;

        for (int x = left; x < right; x++)
        {
            int pixel = pixels[offset + x];

            if (Math.Abs(((pixel >> 16) & 0xFF) - ((color >> 16) & 0xFF)) > StrictTolerance ||
                Math.Abs(((pixel >> 8) & 0xFF) - ((color >> 8) & 0xFF)) > StrictTolerance ||
                Math.Abs((pixel & 0xFF) - (color & 0xFF)) > StrictTolerance)
            {
                return false;
            }
        }

        return true;
    }

    private static long CountInformative(int[][] changedPrefix, int left, int right)
    {
        long count = 0;

        foreach (int[] prefix in changedPrefix)
        {
            count += prefix[right] - prefix[left];
        }

        return count;
    }

    private static bool IsSimilar(int a, int b)
    {
        return Math.Abs(((a >> 16) & 0xFF) - ((b >> 16) & 0xFF)) <= ChannelTolerance &&
            Math.Abs(((a >> 8) & 0xFF) - ((b >> 8) & 0xFF)) <= ChannelTolerance &&
            Math.Abs((a & 0xFF) - (b & 0xFF)) <= ChannelTolerance;
    }

    // Rows that did not change (toolbars, headers, blank space) carry no information.
    private static int[] GetSampleRows(bool[] changedRows, int changedRowCount)
    {
        int step = Math.Max(1, changedRowCount / SampledRows);
        int[] rows = new int[(changedRowCount + step - 1) / step];

        for (int y = 0, seen = 0, index = 0; y < changedRows.Length && index < rows.Length; y++)
        {
            if (changedRows[y] && seen++ % step == 0)
            {
                rows[index++] = y;
            }
        }

        return rows;
    }

    private static int[] ReadPixels(Bitmap bitmap)
    {
        BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
            int[] pixels = new int[bitmap.Width * bitmap.Height];

            for (int y = 0; y < bitmap.Height; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * bitmap.Width, bitmap.Width);
            }

            return pixels;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
