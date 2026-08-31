#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

#nullable enable

using System;
using System.Drawing;

namespace ShareX.ScreenCaptureLib;

public static class ScrollingCaptureImageCombiner
{
    public static Bitmap? Combine(Bitmap result, Bitmap currentImage, ScrollingCaptureDirection direction)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(currentImage);

        return direction switch
        {
            ScrollingCaptureDirection.Horizontal => CombineHorizontal(result, currentImage),
            _ => throw new NotSupportedException("Vertical capture continues to use the legacy combiner.")
        };
    }

    private static Bitmap? CombineHorizontal(Bitmap result, Bitmap currentImage)
    {
        if (result.Height != currentImage.Height)
        {
            return null;
        }

        int sideEdge = GetHorizontalSideEdge(currentImage.Width);
        int contentWidth = currentImage.Width - sideEdge * 2;
        int overlap = FindHorizontalOverlap(result, currentImage, sideEdge, contentWidth);

        if (overlap <= 0 || overlap >= contentWidth)
        {
            return null;
        }

        int appendedWidth = contentWidth - overlap;
        Bitmap combined = new(result.Width + appendedWidth, result.Height);

        using (Graphics graphics = Graphics.FromImage(combined))
        {
            graphics.DrawImage(result,
                new Rectangle(0, 0, result.Width - sideEdge, result.Height),
                new Rectangle(0, 0, result.Width - sideEdge, result.Height),
                GraphicsUnit.Pixel);
            graphics.DrawImage(currentImage,
                new Rectangle(result.Width - sideEdge, 0, appendedWidth, currentImage.Height),
                new Rectangle(sideEdge + overlap, 0, appendedWidth, currentImage.Height),
                GraphicsUnit.Pixel);
            if (sideEdge > 0)
            {
                graphics.DrawImage(currentImage,
                    new Rectangle(result.Width - sideEdge + appendedWidth, 0, sideEdge, currentImage.Height),
                    new Rectangle(currentImage.Width - sideEdge, 0, sideEdge, currentImage.Height),
                    GraphicsUnit.Pixel);
            }
        }

        return combined;
    }

    private static int FindHorizontalOverlap(Bitmap result, Bitmap currentImage, int sideEdge, int contentWidth)
    {
        int maxOverlap = Math.Min(result.Width - sideEdge * 2, contentWidth) - 1;
        int ignoreEdge = Math.Max(50, currentImage.Height / 20);
        ignoreEdge = Math.Min(ignoreEdge, currentImage.Height / 3);
        int startY = ignoreEdge;
        int endY = currentImage.Height - ignoreEdge;

        for (int overlap = maxOverlap; overlap > 0; overlap--)
        {
            bool matches = true;

            for (int x = 0; x < overlap && matches; x++)
            {
                int resultX = result.Width - sideEdge - overlap + x;
                int currentX = sideEdge + x;

                for (int y = startY; y < endY; y++)
                {
                    if (result.GetPixel(resultX, y) != currentImage.GetPixel(currentX, y))
                    {
                        matches = false;
                        break;
                    }
                }
            }

            if (matches)
            {
                return overlap;
            }
        }

        return 0;
    }

    private static int GetHorizontalSideEdge(int width)
    {
        if (width < 300)
        {
            return 0;
        }

        int sideEdge = Math.Max(50, width / 20);
        return Math.Min(sideEdge, width / 3);
    }
}
