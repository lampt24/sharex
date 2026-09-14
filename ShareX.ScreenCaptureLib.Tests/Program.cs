using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using ShareX.ScreenCaptureLib;
using ShareX.ScreenCaptureLib.Presentation.RegionCapture;

if (Array.IndexOf(args, "--scroll-target") >= 0) ScrollingCaptureTargetTests.Run();

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

// Real scrolled frames differ slightly (re-rastered text, a hover highlight following the
// pointer). Stitching must tolerate that instead of ending the capture early.
using Bitmap noisyFirstFrame = CreateContentFrame(0, false, false, false);
using Bitmap noisySecondFrame = CreateContentFrame(90, true, true, false);
using Bitmap? noisyCombined = ScrollingCaptureImageCombiner.Combine(
    noisyFirstFrame, noisySecondFrame, ScrollingCaptureDirection.Horizontal);

Assert(noisyCombined != null, "Slightly different scrolled frames should still combine.");
Assert(noisyCombined!.Width == 690, $"Expected 690px noisy output, got {noisyCombined.Width}px.");

using Bitmap unmovedNoisyFrame = CreateContentFrame(0, true, false, false);
int unmovedShift = ScrollingCaptureImageCombiner.FindHorizontalShift(noisyFirstFrame, unmovedNoisyFrame);
Assert(unmovedShift == 0, $"A noisy frame that did not scroll should report no movement, got {unmovedShift}.");

// Selecting a whole window captures static sidebars and toolbars around the scrolled grid.
// Mostly static frames must still count as moved and stitch at the grid's shift.
using Bitmap chromeFirstFrame = CreateContentFrame(0, false, false, true);
using Bitmap chromeSecondFrame = CreateContentFrame(40, true, false, true);
int chromeShift = ScrollingCaptureImageCombiner.FindHorizontalShift(chromeFirstFrame, chromeSecondFrame);
Assert(chromeShift == 40, $"A scrolled grid inside static chrome should shift 40px, got {chromeShift}.");
using Bitmap? chromeCombined = ScrollingCaptureImageCombiner.CombineHorizontal(
    chromeFirstFrame, chromeFirstFrame, chromeSecondFrame, out Rectangle chromeMoving);
Assert(chromeCombined != null && chromeCombined.Width == 640, "Grid inside static chrome should stitch to 640px.");
Assert(chromeMoving.X >= 350 && chromeMoving.X < 362 && chromeMoving.Y >= 60 && chromeMoving.Y < 72 &&
    chromeMoving.Right == 600 && chromeMoving.Bottom > 180 && chromeMoving.Bottom <= 192,
    $"Moving bounds should exclude the static sidebar, toolbar and scrollbar, got {chromeMoving}.");

// A whole-window capture is rebuilt as a wider window: static rows keep left-anchored items,
// move right-anchored items (like the close button) to the new edge and widen the blank gap.
Color windowBackground = Color.FromArgb(30, 30, 30);
Color leftIcon = Color.FromArgb(200, 60, 60);
Color closeButton = Color.FromArgb(60, 200, 60);
Color stitchedGrid = Color.FromArgb(220, 220, 220);
using Bitmap windowFirstFrame = new(600, 100);
using (Graphics graphics = Graphics.FromImage(windowFirstFrame))
{
    graphics.Clear(windowBackground);
    graphics.FillRectangle(new SolidBrush(leftIcon), 10, 0, 20, 10);
    graphics.FillRectangle(new SolidBrush(closeButton), 550, 0, 30, 10);
}
using Bitmap windowStitched = new(700, 100);
using (Graphics graphics = Graphics.FromImage(windowStitched))
{
    graphics.Clear(Color.Magenta);
    graphics.FillRectangle(new SolidBrush(stitchedGrid), 0, 40, 700, 20);
}
using Bitmap? windowExtended = ScrollingCaptureImageCombiner.ExtendStaticRows(windowStitched, windowFirstFrame, 40, 60);
Assert(windowExtended != null, "Static rows of a whole-window capture should be rebuilt.");
Assert(windowExtended!.GetPixel(20, 5).ToArgb() == leftIcon.ToArgb(), "Left-anchored items should stay in place.");
Assert(windowExtended.GetPixel(660, 5).ToArgb() == closeButton.ToArgb(), "Right-anchored items should move to the new right edge.");
Assert(windowExtended.GetPixel(560, 5).ToArgb() == windowBackground.ToArgb(), "Right-anchored items should not be repeated.");
Assert(windowExtended.GetPixel(400, 80).ToArgb() == windowBackground.ToArgb(), "Blank space below the grid should be widened.");
Assert(windowExtended.GetPixel(650, 50).ToArgb() == stitchedGrid.ToArgb(), "Scrolled rows should keep the stitched content.");

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

Assert(!ScrollingCaptureResultHandling.ShouldShowPreview(new ScrollingCaptureOptions { AutoUpload = true }, true),
    "Auto-uploaded scrolling captures should skip the preview.");
Assert(ScrollingCaptureResultHandling.ShouldShowPreview(new ScrollingCaptureOptions { AutoUpload = false }, true),
    "Manual scrolling captures should show the preview.");
Assert(ScrollingCaptureResultHandling.ShouldShowPreview(new ScrollingCaptureOptions { AutoUpload = true }, false),
    "Failed auto-upload scrolling captures should remain visible for error feedback.");

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

static Bitmap CreateContentFrame(int offset, bool noisy, bool hoverBand, bool staticChrome)
{
    Bitmap bitmap = new(600, 200);

    for (int x = 0; x < bitmap.Width; x++)
    {
        for (int y = 0; y < bitmap.Height; y++)
        {
            int cell = unchecked(((x + offset) / 6 * 73856093) ^ (y / 9 * 19349663));
            // Sparse text on a light background, like a data grid.
            int value = (cell & 0x7fffffff) % 11 == 0 ? 40 : 250;
            if (staticChrome && (x < 350 || y < 60))
            {
                // A static sidebar and toolbar covering most of the frame.
                value = (x * 7 + y * 3) % 5 == 0 ? 90 : 200;
            }
            else if (staticChrome && y >= 192)
            {
                // A horizontal scrollbar whose thumb moves right while the content moves left.
                value = x >= 400 + offset && x < 460 + offset ? 160 : 30;
            }
            if (noisy)
            {
                // Slight re-rasterisation everywhere.
                value = Math.Min(255, value + 5);
            }
            if (hoverBand && y >= 100 && y < 104)
            {
                value = 120;
            }
            bitmap.SetPixel(x, y, Color.FromArgb(value, value, value));
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
