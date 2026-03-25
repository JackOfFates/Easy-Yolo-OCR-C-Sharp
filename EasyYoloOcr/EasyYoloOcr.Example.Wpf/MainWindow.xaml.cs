using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EasyYoloOcr.Core;
using OpenCvSharp;

namespace EasyYoloOcr.Example.Wpf;

public partial class MainWindow : System.Windows.Window
{
    private ScreenCapture? _capture;
    private DispatcherTimer? _timer;
    private OcrEngine? _ocrEngine;
    private volatile bool _isOcrRunning;
    private readonly Stopwatch _fpsWatch = new();
    private int _frameCount;

    private readonly object _frameLock = new();
    private byte[]? _latestFrameForOcr;
    private int _latestFrameW, _latestFrameH;

    private readonly object _overlayLock = new();
    private List<TextRegion>? _overlayRegions;
    private int _overlayOffsetX, _overlayOffsetY;
    private Mat? _overlayMat;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_capture == null)
            StartCapture();
        else
            StopCapture();
    }

    private void StartCapture()
    {
        try
        {
            _capture = new ScreenCapture();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start screen capture:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            _ocrEngine = new OcrEngine("eng");
        }
        catch (Exception ex)
        {
            // OCR is optional — capture still works without tessdata
            _ocrEngine = null;
            OcrTextLabel.Text = $"OCR unavailable: {ex.Message}\n"
                + "Place tessdata files (e.g. eng.traineddata) in a 'tessdata' folder next to the executable.\n"
                + "Download from: https://github.com/tesseract-ocr/tessdata";
        }

        // Fast timer for smooth preview; OCR runs independently
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += OnCaptureTick;
        _timer.Start();

        _fpsWatch.Restart();
        _frameCount = 0;
        StartStopButton.Content = "Stop Capture";
    }

    private void StopCapture()
    {
        _timer?.Stop();
        _timer = null;
        _capture?.Dispose();
        _capture = null;
        _ocrEngine?.Dispose();
        _ocrEngine = null;

        lock (_frameLock) _latestFrameForOcr = null;
        lock (_overlayLock) _overlayRegions = null;
        _overlayMat?.Dispose();
        _overlayMat = null;

        StartStopButton.Content = "Start Capture";
        FpsLabel.Text = string.Empty;
        OcrTextLabel.Text = string.Empty;
    }

    private void OnCaptureTick(object? sender, EventArgs e)
    {
        if (_capture == null) return;

        try
        {
            byte[]? frame = _capture.TryCaptureFrame();
            if (frame == null) return;

            int w = _capture.Width;
            int h = _capture.Height;

            // Reuse Mat to avoid per-frame allocation
            if (_overlayMat == null || _overlayMat.Rows != h || _overlayMat.Cols != w)
            {
                _overlayMat?.Dispose();
                _overlayMat = new Mat(h, w, MatType.CV_8UC4);
            }
            Marshal.Copy(frame, 0, _overlayMat.Data, frame.Length);

            // Draw black box over MainWindow area
            IntPtr mainHwnd = new WindowInteropHelper(this).Handle;
            if (GetWindowRect(mainHwnd, out RECT mainRect))
            {
                int mL = Math.Max(0, mainRect.Left);
                int mT = Math.Max(0, mainRect.Top);
                int mR = Math.Min(w, mainRect.Right);
                int mB = Math.Min(h, mainRect.Bottom);
                if (mR > mL && mB > mT)
                {
                    Cv2.Rectangle(_overlayMat,
                        new OpenCvSharp.Point(mL, mT), new OpenCvSharp.Point(mR, mB),
                        new Scalar(0, 0, 0, 255), -1);
                }
            }

            // Draw OCR text regions from latest results
            List<TextRegion>? regions;
            int offsetX, offsetY;
            lock (_overlayLock)
            {
                regions = _overlayRegions;
                offsetX = _overlayOffsetX;
                offsetY = _overlayOffsetY;
            }

            if (regions != null)
            {
                const double fontScale = 0.5;
                const int thickness = 1;
                foreach (var region in regions)
                {
                    int rx = region.X + offsetX;
                    int ry = region.Y + offsetY;
                    int rw = region.Width;
                    int rh = region.Height;

                    // Red border around text region
                    Cv2.Rectangle(_overlayMat,
                        new OpenCvSharp.Point(rx, ry),
                        new OpenCvSharp.Point(rx + rw, ry + rh),
                        new Scalar(0, 0, 255, 255), 2);

                    // Strip non-ASCII characters that Hershey fonts cannot render
                    string label = new string(region.Text.Where(c => c >= 32 && c < 127).ToArray());
                    if (string.IsNullOrWhiteSpace(label)) continue;
                    if (label.Length > 80) label = label[..80] + "...";

                    var textSize = Cv2.GetTextSize(label, HersheyFonts.HersheySimplex,
                        fontScale, thickness, out int baseline);

                    // Position label above the box
                    int labelY = ry - textSize.Height - 6;
                    if (labelY < 0) labelY = ry + rh + 2;

                    // Red background for label
                    Cv2.Rectangle(_overlayMat,
                        new OpenCvSharp.Point(rx, labelY),
                        new OpenCvSharp.Point(rx + textSize.Width + 4, labelY + textSize.Height + baseline + 4),
                        new Scalar(0, 0, 255, 255), -1);

                    // Black text
                    Cv2.PutText(_overlayMat, label,
                        new OpenCvSharp.Point(rx + 2, labelY + textSize.Height + 2),
                        HersheyFonts.HersheySimplex, fontScale,
                        new Scalar(0, 0, 0, 255), thickness);
                }
            }

            // Create BitmapSource from annotated Mat
            var bmp = BitmapSource.Create(w, h, 96, 96,
                PixelFormats.Bgra32, null, _overlayMat.Data, w * h * 4, w * 4);
            bmp.Freeze();
            ScreenImage.Source = bmp;

            // FPS counter
            _frameCount++;
            if (_fpsWatch.Elapsed.TotalSeconds >= 1.0)
            {
                FpsLabel.Text = $"{_frameCount / _fpsWatch.Elapsed.TotalSeconds:F1} FPS";
                _frameCount = 0;
                _fpsWatch.Restart();
            }

            // Hand off a copy of the frame for background OCR (non-blocking)
            if (!_isOcrRunning && _ocrEngine != null)
            {
                byte[] frameCopy = new byte[frame.Length];
                Buffer.BlockCopy(frame, 0, frameCopy, 0, frame.Length);
                lock (_frameLock)
                {
                    _latestFrameForOcr = frameCopy;
                    _latestFrameW = w;
                    _latestFrameH = h;
                }
                _isOcrRunning = true;
                _ = RunOcrAsync();
            }
        }
        catch (Exception ex)
        {
            OcrTextLabel.Text = $"Error: {ex.Message}";
        }
    }

    private async Task RunOcrAsync()
    {
        try
        {
            byte[]? frameCopy;
            int w, h;
            lock (_frameLock)
            {
                frameCopy = _latestFrameForOcr;
                w = _latestFrameW;
                h = _latestFrameH;
                _latestFrameForOcr = null;
            }

            if (frameCopy == null) return;

            var ocr = _ocrEngine;
            if (ocr == null) return;

            // Gather window rects on the UI thread before going to background
            IntPtr mainHwnd = new WindowInteropHelper(this).Handle;
            GetWindowRect(mainHwnd, out RECT mainRect);

            IntPtr fgHwnd = GetForegroundWindow();
            RECT fgRect = default;
            bool hasForeground = fgHwnd != IntPtr.Zero
                                 && fgHwnd != mainHwnd
                                 && GetWindowRect(fgHwnd, out fgRect);

            var (regions, offsetX, offsetY, fullText) = await Task.Run(() =>
            {
                using var mat = new Mat(h, w, MatType.CV_8UC4);
                Marshal.Copy(frameCopy, 0, mat.Data, frameCopy.Length);

                // Black out the MainWindow area so OCR doesn't read its own UI
                int mLeft = Math.Max(0, mainRect.Left);
                int mTop = Math.Max(0, mainRect.Top);
                int mRight = Math.Min(w, mainRect.Right);
                int mBottom = Math.Min(h, mainRect.Bottom);
                if (mRight > mLeft && mBottom > mTop)
                {
                    var mask = new OpenCvSharp.Rect(mLeft, mTop, mRight - mLeft, mBottom - mTop);
                    using var roi = new Mat(mat, mask);
                    roi.SetTo(new Scalar(0, 0, 0, 255));
                }

                // If a different window is in the foreground, crop to it for focused OCR
                Mat target = mat;
                bool disposeTarget = false;
                int ox = 0, oy = 0;
                if (hasForeground)
                {
                    int fLeft = Math.Max(0, fgRect.Left);
                    int fTop = Math.Max(0, fgRect.Top);
                    int fRight = Math.Min(w, fgRect.Right);
                    int fBottom = Math.Min(h, fgRect.Bottom);
                    if (fRight > fLeft && fBottom > fTop)
                    {
                        var fgCrop = new OpenCvSharp.Rect(fLeft, fTop, fRight - fLeft, fBottom - fTop);
                        target = new Mat(mat, fgCrop).Clone();
                        disposeTarget = true;
                        ox = fLeft;
                        oy = fTop;
                    }
                }

                try
                {
                    using var gray = new Mat();
                    Cv2.CvtColor(target, gray, ColorConversionCodes.BGRA2GRAY);
                    var boxes = ocr.RecognizeWithBoxes(gray);
                    string text = string.Join("\n", boxes.Select(b => b.Text));
                    return (boxes, ox, oy, text);
                }
                finally
                {
                    if (disposeTarget) target.Dispose();
                }
            });

            // Store overlay data for the preview renderer
            lock (_overlayLock)
            {
                _overlayRegions = regions;
                _overlayOffsetX = offsetX;
                _overlayOffsetY = offsetY;
            }

            Dispatcher.Invoke(() =>
            {
                OcrTextLabel.Text = string.IsNullOrWhiteSpace(fullText)
                    ? "(no text detected)"
                    : fullText;
            });
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() => OcrTextLabel.Text = $"Error: {ex.Message}");
        }
        finally
        {
            _isOcrRunning = false;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        StopCapture();
        base.OnClosed(e);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }
}
