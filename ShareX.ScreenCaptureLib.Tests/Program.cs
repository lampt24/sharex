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

// Editors scroll by a fractional line height, so rows holding the same text come back
// re-rastered rather than byte-identical. Vertical stitching has to match them anyway;
// comparing rows exactly ends the capture after a single frame.
const int verticalWidth = 400;
const int verticalHeight = 300;
const int verticalLeft = 50;
const int verticalRight = 350;
// Matches the manager: max(50, height / 10) plus the 20 static rows of the status bar.
const int verticalAnchor = verticalHeight - 70 - 1;
const int verticalShift = 60;

using Bitmap verticalFirstFrame = CreateVerticalFrame(0, false);
using Bitmap verticalSecondFrame = CreateVerticalFrame(verticalShift, true);
int[] verticalTail = ScrollingCaptureImageCombiner.ReadPixels(verticalFirstFrame);
int[] verticalCurrent = ScrollingCaptureImageCombiner.ReadPixels(verticalSecondFrame);
int verticalExpectedIndex = verticalAnchor - verticalShift;

Assert(!RowsAreIdentical(verticalTail, verticalAnchor, verticalCurrent, verticalExpectedIndex, verticalWidth, verticalLeft, verticalRight),
    "The fixture must re-raster rows, otherwise it does not reproduce byte-exact matching.");
Assert(ScrollingCaptureImageCombiner.IsMatchingRow(verticalTail, verticalAnchor, verticalCurrent, verticalExpectedIndex,
    verticalWidth, verticalLeft, verticalRight), "Re-rastered rows holding the same text should match.");
Assert(!ScrollingCaptureImageCombiner.IsMatchingRow(verticalTail, verticalAnchor, verticalCurrent, verticalExpectedIndex + 1,
    verticalWidth, verticalLeft, verticalRight), "Rows holding different text should not match.");

(int verticalMatchCount, int verticalMatchIndex) = ScrollingCaptureImageCombiner.FindVerticalOverlap(
    verticalTail, verticalCurrent, verticalWidth, verticalHeight, verticalLeft, verticalRight,
    verticalAnchor, verticalHeight / 2);

Assert(verticalMatchIndex == verticalExpectedIndex,
    $"Expected the anchor row to line up at row {verticalExpectedIndex}, got {verticalMatchIndex}.");
Assert(verticalMatchCount > 100, $"Expected a long run of matching rows, got {verticalMatchCount}.");

// Static chrome alone must never look like an overlap; an unscrolled frame has to stay put.
using Bitmap verticalUnmovedFrame = CreateVerticalFrame(0, true);
(_, int unmovedIndex) = ScrollingCaptureImageCombiner.FindVerticalOverlap(
    verticalTail, ScrollingCaptureImageCombiner.ReadPixels(verticalUnmovedFrame),
    verticalWidth, verticalHeight, verticalLeft, verticalRight, verticalAnchor, verticalHeight / 2);
Assert(unmovedIndex == verticalAnchor, $"A frame that did not scroll should line up at the anchor, got {unmovedIndex}.");

Console.WriteLine("Vertical scrolling capture image tests passed.");

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

ScrollingCaptureOptions autoUpload = new() { AutoUpload = true };
ScrollingCaptureOptions manualUpload = new() { AutoUpload = false };

Assert(!ScrollingCaptureResultHandling.ShouldShowPreview(autoUpload, true, ScrollingCaptureStatus.Successful),
    "Auto-uploaded scrolling captures should skip the preview.");
Assert(ScrollingCaptureResultHandling.ShouldShowPreview(manualUpload, true, ScrollingCaptureStatus.Successful),
    "Manual scrolling captures should show the preview.");
Assert(ScrollingCaptureResultHandling.ShouldShowPreview(autoUpload, false, ScrollingCaptureStatus.Failed),
    "Failed auto-upload scrolling captures should remain visible for error feedback.");

// A capture that never scrolled is one screen, not a page: show it instead of uploading it.
Assert(!ScrollingCaptureResultHandling.ShouldAutoUpload(autoUpload, true, ScrollingCaptureStatus.NotScrolled),
    "A capture that never scrolled should not be auto-uploaded.");
Assert(ScrollingCaptureResultHandling.ShouldShowPreview(autoUpload, true, ScrollingCaptureStatus.NotScrolled),
    "A capture that never scrolled should stay visible even with auto-upload on.");
Assert(ScrollingCaptureResultHandling.ShouldAutoUpload(autoUpload, true, ScrollingCaptureStatus.PartiallySuccessful),
    "A partial capture still has stitched content worth uploading.");

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

// A navigation bar above the text and a status bar below it never scroll, like a document
// pane in an IDE. Content rows show the document row that a scroll of `offset` brings up.
static Bitmap CreateVerticalFrame(int offset, bool reRastered)
{
    Bitmap bitmap = new(400, 300);

    for (int y = 0; y < bitmap.Height; y++)
    {
        bool staticRow = y < 20 || y >= 280;
        int documentRow = staticRow ? y : y - 20 + offset;

        for (int x = 0; x < bitmap.Width; x++)
        {
            int cell = unchecked(documentRow * 19349663 ^ x / 3 * 73856093);
            int value = staticRow ? 70 + x % 5 : (cell & 0x7fffffff) % 7 == 0 ? 40 : 250;

            // Anti-aliasing lands on a different pixel of the row after a fractional scroll.
            if (reRastered && !staticRow && x % 137 == documentRow % 137)
            {
                value = 255 - value;
            }

            bitmap.SetPixel(x, y, Color.FromArgb(value, value, value));
        }
    }

    return bitmap;
}

static bool RowsAreIdentical(int[] first, int firstRow, int[] second, int secondRow, int width, int left, int right)
{
    for (int x = left; x < right; x++)
    {
        if (first[firstRow * width + x] != second[secondRow * width + x])
        {
            return false;
        }
    }

    return true;
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
