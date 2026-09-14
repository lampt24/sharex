#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using ShareX.HelpersLib;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ShareX.ScreenCaptureLib
{
    internal class ScrollingCaptureManager : IDisposable
    {
        public ScrollingCaptureOptions Options { get; private set; }
        public Bitmap Result { get; private set; }
        public bool IsCapturing { get; private set; }

        private readonly ScrollingCaptureDirection direction;
        private Bitmap lastScreenshot;
        private Bitmap previousScreenshot;
        private bool stopRequested;
        private ScrollingCaptureStatus status;
        private int bestMatchCount, bestMatchIndex, bestIgnoreBottomOffset;
        private WindowInfo selectedWindow;
        private Rectangle selectedRectangle;
        private ScrollingCaptureTarget scrollTarget;
        private bool observedMovement;
        // Union of the frame areas that moved during a horizontal capture.
        private Rectangle movingBounds;
        private Bitmap firstScreenshot;

        public ScrollingCaptureManager(ScrollingCaptureOptions options, ScrollingCaptureDirection direction = ScrollingCaptureDirection.Vertical)
        {
            Options = options;
            this.direction = direction;
        }

        public void Dispose()
        {
            Reset();
        }

        private void Reset(bool keepResult = false)
        {
            if (lastScreenshot != null)
            {
                lastScreenshot.Dispose();
                lastScreenshot = null;
            }

            if (previousScreenshot != null)
            {
                previousScreenshot.Dispose();
                previousScreenshot = null;
            }

            if (firstScreenshot != null)
            {
                firstScreenshot.Dispose();
                firstScreenshot = null;
            }

            if (!keepResult && Result != null)
            {
                Result.Dispose();
                Result = null;
            }
        }

        public async Task<ScrollingCaptureStatus> StartCapture()
        {
            if (!IsCapturing && selectedWindow != null && !selectedRectangle.IsEmpty)
            {
                IsCapturing = true;
                stopRequested = false;
                NativeMethods.GetAsyncKeyState((int)VirtualKeyCode.ESCAPE);
                status = ScrollingCaptureStatus.Failed;
                bestMatchCount = 0;
                bestMatchIndex = 0;
                bestIgnoreBottomOffset = 0;
                Reset();
                observedMovement = false;
                movingBounds = Rectangle.Empty;

                ScrollingCaptureRegionWindow regionWindow = null;

                if (Options.ShowRegion)
                {
                    regionWindow = new ScrollingCaptureRegionWindow(selectedRectangle);
                    regionWindow.Show();
                }

                try
                {
                    selectedWindow.Activate();

                    await Task.Delay(Options.StartDelay);
                    scrollTarget.PositionPointer();

                    if (Options.AutoScrollTop)
                    {
                        if (scrollTarget.SupportsAutomation)
                        {
                            await Task.Run(scrollTarget.ScrollToStart);
                        }
                        else if (direction == ScrollingCaptureDirection.Horizontal)
                        {
                            scrollTarget.SendMessage((int)WindowsMessages.HSCROLL, (int)ScrollBarCommands.SB_LEFT);
                        }
                        else
                        {
                            scrollTarget.SendMessage((int)WindowsMessages.VSCROLL, (int)ScrollBarCommands.SB_TOP);
                        }

                        await Task.Delay(Options.ScrollDelay);
                    }

                    Screenshot screenshot = new Screenshot()
                    {
                        CaptureCursor = false
                    };

                    while (!IsStopRequested())
                    {
                        lastScreenshot = screenshot.CaptureRectangle(selectedRectangle);

                        if (await IsFrameUnchangedAsync())
                        {
                            // A single unchanged frame mid-way can be a dropped scroll or a slow
                            // repaint. Before the first movement, only wait; afterwards scroll again
                            // and end only when that still moves nothing.
                            if (observedMovement) await ScrollAsync();
                            await Task.Delay(Math.Max(500, Options.ScrollDelay));
                            if (IsStopRequested()) break;
                            lastScreenshot?.Dispose();
                            lastScreenshot = screenshot.CaptureRectangle(selectedRectangle);
                            if (await IsFrameUnchangedAsync())
                            {
                                if (observedMovement) break;
                                Reset();
                                throw new InvalidOperationException(ScrollingCapturePointWindow.Text("ScrollingCaptureWindow_No_scroll_movement"));
                            }
                        }
                        if (previousScreenshot != null) observedMovement = true;

                        await ScrollAsync();

                        Stopwatch timer = Stopwatch.StartNew();

                        if (lastScreenshot != null)
                        {
                            if (Result == null && direction == ScrollingCaptureDirection.Horizontal)
                            {
                                firstScreenshot?.Dispose();
                                firstScreenshot = (Bitmap)lastScreenshot.Clone();
                            }

                            Bitmap newResult = await CombineImagesAsync(Result, lastScreenshot);

                            if (newResult != null)
                            {
                                Result?.Dispose();
                                Result = newResult;
                            }
                            else
                            {
                                // Frames no longer line up; keep what was stitched but do not report it as complete.
                                if (direction == ScrollingCaptureDirection.Horizontal && Result != null)
                                {
                                    status = ScrollingCaptureStatus.PartiallySuccessful;
                                }

                                break;
                            }
                        }

                        if (IsStopRequested())
                        {
                            break;
                        }

                        if (lastScreenshot != null)
                        {
                            if (previousScreenshot != null)
                            {
                                previousScreenshot.Dispose();
                            }

                            previousScreenshot = lastScreenshot;
                            lastScreenshot = null;
                        }

                        int delay = ScrollingCaptureDelay.GetPostScrollDelay(
                            Options.ScrollDelay, (int)timer.ElapsedMilliseconds, direction);

                        if (delay > 0)
                        {
                            await Task.Delay(delay);
                        }
                    }

                    await ExtendStaticRowsAsync();
                }
                catch
                {
                    status = ScrollingCaptureStatus.Failed;
                    Reset();
                    throw;
                }
                finally
                {
                    regionWindow?.Close();

                    Reset(true);
                    IsCapturing = false;
                }
            }

            return status;
        }

        public void StopCapture()
        {
            if (IsCapturing)
            {
                stopRequested = true;
            }
        }

        public async Task<bool> SelectWindowAsync()
        {
            ScrollingCaptureTarget target = await ScrollingCapturePointWindow.SelectAsync(direction);
            if (target == null) return false;
            await Task.Delay(100);
            SetTarget(target);
            return scrollTarget.Handle != IntPtr.Zero;
        }

        internal void SetTarget(ScrollingCaptureTarget target)
        {
            scrollTarget = target;
            selectedRectangle = scrollTarget.Bounds;
            selectedWindow = new WindowInfo(scrollTarget.RootHandle);
        }

        private bool IsScrollReachedBottom(IntPtr handle)
        {
            SCROLLINFO scrollInfo = new SCROLLINFO();
            scrollInfo.cbSize = (uint)Marshal.SizeOf(scrollInfo);
            scrollInfo.fMask = (uint)(ScrollInfoMask.SIF_RANGE | ScrollInfoMask.SIF_PAGE | ScrollInfoMask.SIF_TRACKPOS);

            if (NativeMethods.GetScrollInfo(handle, (int)SBOrientation.SB_VERT, ref scrollInfo))
            {
                return scrollInfo.nMax == scrollInfo.nTrackPos + scrollInfo.nPage - 1;
            }

            return CompareLastTwoImages();
        }

        // Apps without UI Automation panels (e.g. JetBrains IDEs) are captured as a whole window.
        // Only the scrolled rows are stitched; the rest is rebuilt as a wider window so its
        // title bar, toolbars and status bar are not repeated.
        private async Task ExtendStaticRowsAsync()
        {
            if (direction != ScrollingCaptureDirection.Horizontal || Result == null || firstScreenshot == null || movingBounds.IsEmpty)
            {
                return;
            }

            Bitmap result = Result, first = firstScreenshot;
            Rectangle band = movingBounds;
            Bitmap extended = await Task.Run(() => ScrollingCaptureImageCombiner.ExtendStaticRows(result, first, band.Top, band.Bottom));

            if (extended != null)
            {
                Result.Dispose();
                Result = extended;
            }
        }

        private async Task ScrollAsync()
        {
            scrollTarget.PositionPointer();

            if (scrollTarget.SupportsAutomation)
            {
                await Task.Run(() => scrollTarget.Scroll(Options.ScrollAmount));
            }
            else if (direction == ScrollingCaptureDirection.Horizontal)
            {
                ScrollHorizontally();
            }
            else
            {
                ScrollVertically();
            }
        }

        private async Task<bool> IsFrameUnchangedAsync()
        {
            if (CompareLastTwoImages()) return true;
            if (direction != ScrollingCaptureDirection.Horizontal || lastScreenshot == null || previousScreenshot == null) return false;

            // Hover effects or re-rastered text make frames differ slightly without any scrolling.
            Bitmap previous = previousScreenshot, last = lastScreenshot;
            return await Task.Run(() => ScrollingCaptureImageCombiner.FindHorizontalShift(previous, last)) == 0;
        }

        private bool CompareLastTwoImages()
        {
            if (lastScreenshot != null && previousScreenshot != null)
            {
                return ImageHelpers.CompareImages(lastScreenshot, previousScreenshot);
            }

            return false;
        }

        private async Task<Bitmap> CombineImagesAsync(Bitmap result, Bitmap currentImage)
        {
            if (direction == ScrollingCaptureDirection.Horizontal && result != null)
            {
                Bitmap previous = previousScreenshot;
                (Bitmap combined, Rectangle moving) = await Task.Run(() =>
                {
                    Bitmap bitmap = ScrollingCaptureImageCombiner.CombineHorizontal(result, previous, currentImage, out Rectangle bounds);
                    return (bitmap, bounds);
                });

                if (combined != null)
                {
                    movingBounds = movingBounds.IsEmpty ? moving : Rectangle.Union(movingBounds, moving);
                }

                return combined;
            }

            return await Task.Run(() => CombineImages(result, currentImage));
        }

        private bool IsStopRequested()
        {
            if (!stopRequested && (NativeMethods.GetAsyncKeyState((int)VirtualKeyCode.ESCAPE) & 1) != 0)
            {
                stopRequested = true;
            }

            return stopRequested;
        }

        private void ScrollVertically()
        {
            switch (Options.ScrollMethod)
            {
                case ScrollMethod.MouseWheel:
                    scrollTarget.ScrollVerticalWheel(Options.ScrollAmount);
                    break;
                case ScrollMethod.DownArrow:
                    for (int i = 0; i < Options.ScrollAmount; i++)
                    {
                        scrollTarget.SendKey(VirtualKeyCode.DOWN);
                    }
                    break;
                case ScrollMethod.PageDown:
                    scrollTarget.SendKey(VirtualKeyCode.NEXT);
                    break;
                case ScrollMethod.ScrollMessage:
                    for (int i = 0; i < Options.ScrollAmount; i++)
                    {
                        scrollTarget.SendMessage((int)WindowsMessages.VSCROLL, (int)ScrollBarCommands.SB_LINEDOWN);
                    }
                    break;
            }
        }

        private void ScrollHorizontally()
        {
            HorizontalScrollStrategy strategy = HorizontalScrollStrategySelector.Select(selectedWindow.ProcessName);

            if (strategy == HorizontalScrollStrategy.ExcelScrollBarButton)
            {
                if (ExcelHorizontalScroller.ScrollColumnRight(selectedWindow.Handle, scrollTarget.Point)) return;
            }

            for (int i = 0; i < Options.ScrollAmount; i++)
            {
                scrollTarget.ScrollHorizontalWheel();
            }
        }

        private Bitmap CombineImages(Bitmap result, Bitmap currentImage)
        {
            if (result == null)
            {
                status = ScrollingCaptureStatus.Successful;

                return (Bitmap)currentImage.Clone();
            }

            int matchCount = 0;
            int matchIndex = 0;
            int matchLimit = currentImage.Height / 2;

            int ignoreSideOffset = Math.Max(50, currentImage.Width / 20);
            ignoreSideOffset = Math.Min(ignoreSideOffset, currentImage.Width / 3);

            Rectangle rect = new Rectangle(ignoreSideOffset, result.Height - currentImage.Height, currentImage.Width - ignoreSideOffset * 2, currentImage.Height);

            BitmapData bdResult = result.LockBits(new Rectangle(0, 0, result.Width, result.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData bdCurrentImage = currentImage.LockBits(new Rectangle(0, 0, currentImage.Width, currentImage.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int stride = bdResult.Stride;
            int pixelSize = stride / result.Width;
            IntPtr resultScan0 = bdResult.Scan0 + pixelSize * ignoreSideOffset;
            IntPtr currentImageScan0 = bdCurrentImage.Scan0 + pixelSize * ignoreSideOffset;
            int compareLength = pixelSize * rect.Width;

            int ignoreBottomOffsetMax = currentImage.Height / 3;
            int ignoreBottomOffset = Math.Max(50, currentImage.Height / 10);

            if (Options.AutoIgnoreBottomEdge)
            {
                IntPtr resultScan0Last = resultScan0 + (result.Height - 1) * stride;
                IntPtr currentImageScan0Last = currentImageScan0 + (currentImage.Height - 1) * stride;

                for (int i = 0; i <= ignoreBottomOffsetMax; i++)
                {
                    if (NativeMethods.memcmp(resultScan0Last - i * stride, currentImageScan0Last - i * stride, compareLength) != 0)
                    {
                        ignoreBottomOffset += i;
                        break;
                    }
                }

                ignoreBottomOffset = Math.Max(ignoreBottomOffset, bestIgnoreBottomOffset);
            }

            ignoreBottomOffset = Math.Min(ignoreBottomOffset, ignoreBottomOffsetMax);

            int rectBottom = rect.Bottom - ignoreBottomOffset - 1;

            for (int currentImageY = currentImage.Height - 1; currentImageY >= 0 && matchCount < matchLimit; currentImageY--)
            {
                int currentMatchCount = 0;

                for (int y = 0; currentImageY - y >= 0 && currentMatchCount < matchLimit; y++)
                {
                    if (NativeMethods.memcmp(resultScan0 + ((rectBottom - y) * stride), currentImageScan0 + ((currentImageY - y) * stride), compareLength) == 0)
                    {
                        currentMatchCount++;
                    }
                    else
                    {
                        break;
                    }
                }

                if (currentMatchCount > matchCount)
                {
                    matchCount = currentMatchCount;
                    matchIndex = currentImageY;
                }
            }

            result.UnlockBits(bdResult);
            currentImage.UnlockBits(bdCurrentImage);

            bool bestGuess = false;

            if (matchCount == 0 && bestMatchCount > 0)
            {
                matchCount = bestMatchCount;
                matchIndex = bestMatchIndex;
                ignoreBottomOffset = bestIgnoreBottomOffset;
                bestGuess = true;
            }

            if (matchCount > 0)
            {
                int matchHeight = currentImage.Height - matchIndex - 1;

                if (matchHeight > 0)
                {
                    if (matchCount > bestMatchCount)
                    {
                        bestMatchCount = matchCount;
                        bestMatchIndex = matchIndex;
                        bestIgnoreBottomOffset = ignoreBottomOffset;
                    }

                    Bitmap newResult = new Bitmap(result.Width, result.Height - ignoreBottomOffset + matchHeight);

                    using (Graphics g = Graphics.FromImage(newResult))
                    {
                        g.CompositingMode = CompositingMode.SourceCopy;
                        g.InterpolationMode = InterpolationMode.NearestNeighbor;

                        g.DrawImage(result, new Rectangle(0, 0, result.Width, result.Height - ignoreBottomOffset),
                            new Rectangle(0, 0, result.Width, result.Height - ignoreBottomOffset), GraphicsUnit.Pixel);
                        g.DrawImage(currentImage, new Rectangle(0, result.Height - ignoreBottomOffset, currentImage.Width, matchHeight),
                            new Rectangle(0, matchIndex + 1, currentImage.Width, matchHeight), GraphicsUnit.Pixel);
                    }

                    if (bestGuess)
                    {
                        status = ScrollingCaptureStatus.PartiallySuccessful;
                    }
                    else if (status != ScrollingCaptureStatus.PartiallySuccessful)
                    {
                        status = ScrollingCaptureStatus.Successful;
                    }

                    return newResult;
                }
            }

            status = ScrollingCaptureStatus.Failed;

            return null;
        }
    }
}
