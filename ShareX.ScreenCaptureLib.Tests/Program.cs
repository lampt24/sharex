using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using ShareX.ScreenCaptureLib;
using ShareX.ScreenCaptureLib.Presentation.RegionCapture;

using Bitmap source = CreateStripedBitmap(Color.Red, Color.Green, Color.Blue, Color.Yellow);
using Bitmap firstFrame = source.Clone(new Rectangle(0, 0, 3, 2), source.PixelFormat);
using Bitmap secondFrame = source.Clone(new Rectangle(1, 0, 3, 2), source.PixelFormat);
using Bitmap? combined = ScrollingCaptureImageCombiner.Combine(firstFrame, secondFrame, ScrollingCaptureDirection.Horizontal);

Assert(combined != null, "Horizontal frames with overlap should combine.");
Assert(combined!.Size == new Size(4, 2), $"Expected 4x2 output, got {combined.Size}.");

for (int x = 0; x < source.Width; x++)
{
    Assert(combined.GetPixel(x, 0).ToArgb() == source.GetPixel(x, 0).ToArgb(), $"Unexpected pixel at x={x}.");
}

Color[] excelContent = new Color[220];
for (int x = 0; x < excelContent.Length; x++)
{
    excelContent[x] = Color.FromArgb(255, x * 37 % 256, x * 67 % 256, x * 97 % 256);
}

using Bitmap excelFirstFrame = CreateExcelFrame(excelContent, 0);
using Bitmap excelSecondFrame = CreateExcelFrame(excelContent, 20);
using Bitmap? excelCombined = ScrollingCaptureImageCombiner.Combine(
    excelFirstFrame, excelSecondFrame, ScrollingCaptureDirection.Horizontal);

Assert(excelCombined != null, "Excel frames with fixed side chrome should combine.");
Assert(excelCombined!.Size == new Size(320, 12), $"Expected 320x12 Excel output, got {excelCombined.Size}.");
for (int x = 0; x < excelContent.Length; x++)
{
    Assert(excelCombined.GetPixel(50 + x, 6).ToArgb() == excelContent[x].ToArgb(),
        $"Unexpected Excel content pixel at x={x}.");
}

Console.WriteLine("Horizontal scrolling capture image tests passed.");

HorizontalScrollStrategy excelStrategy = HorizontalScrollStrategySelector.Select("EXCEL");
Assert(excelStrategy == HorizontalScrollStrategy.ExcelScrollBarButton,
    $"Expected Excel scrollbar button scrolling, got {excelStrategy}.");

int horizontalDelay = ScrollingCaptureDelay.GetPostScrollDelay(300, 500, ScrollingCaptureDirection.Horizontal);
Assert(horizontalDelay == 300, $"Expected full horizontal delay, got {horizontalDelay} ms.");

Console.WriteLine("Horizontal scrolling strategy tests passed.");

Assert(ScrollingCaptureStopKey.ShouldStop(Keys.Escape, true),
    "Escape should stop an active scrolling capture.");
Assert(!ScrollingCaptureStopKey.ShouldStop(Keys.Enter, true),
    "Non-Escape keys should not stop scrolling capture.");
Assert(!ScrollingCaptureStopKey.ShouldStop(Keys.Escape, false),
    "Escape should not be intercepted when capture is inactive.");

Console.WriteLine("Scrolling capture stop-key tests passed.");

RegionSelectionOverlay regionOverlay = new();
regionOverlay.DimAlpha = 89;
Avalonia.Media.SolidColorBrush dimBrush = (Avalonia.Media.SolidColorBrush)typeof(RegionSelectionOverlay)
    .GetField("_dimBrush", BindingFlags.Instance | BindingFlags.NonPublic)!
    .GetValue(regionOverlay)!;
Assert(dimBrush.Color == Avalonia.Media.Color.FromArgb(89, 128, 128, 128),
    $"Expected a 35% gray dim overlay, got {dimBrush.Color}.");
Assert(new RegionCaptureOptions().BackgroundDimStrength == 35,
    "Expected the default region capture dim strength to be 35%.");

Console.WriteLine("Region capture overlay appearance tests passed.");

static Bitmap CreateStripedBitmap(params Color[] colors)
{
    Bitmap bitmap = new(colors.Length, 2);

    for (int x = 0; x < colors.Length; x++)
    {
        for (int y = 0; y < bitmap.Height; y++)
        {
            bitmap.SetPixel(x, y, colors[x]);
        }
    }

    return bitmap;
}

static Bitmap CreateExcelFrame(Color[] content, int offset)
{
    Bitmap bitmap = new(300, 12);

    using (Graphics graphics = Graphics.FromImage(bitmap))
    {
        graphics.Clear(Color.Gray);
        graphics.FillRectangle(Brushes.DarkGray, 250, 0, 50, bitmap.Height);
    }

    for (int x = 0; x < 200; x++)
    {
        for (int y = 0; y < bitmap.Height; y++)
        {
            bitmap.SetPixel(50 + x, y, content[offset + x]);
        }
    }

    return bitmap;
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
