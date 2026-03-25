using Tesseract;

namespace EasyYoloOcr.Core;

/// <summary>
/// A recognized text region with its bounding box.
/// </summary>
public record struct TextRegion(int X, int Y, int Width, int Height, string Text);

/// <summary>
/// OCR engine using Tesseract. Replaces EasyOCR Reader from the Python project.
/// 
/// NOTE: You need Tesseract trained data files (tessdata) in a 'tessdata' directory
/// next to the executable, or set the TESSDATA_PREFIX environment variable.
/// Download from: https://github.com/tesseract-ocr/tessdata
/// </summary>
public class OcrEngine : IDisposable
{
    private readonly TesseractEngine _engine;

    /// <summary>
    /// Initialize the OCR engine with the specified language(s).
    /// </summary>
    /// <param name="language">Tesseract language string, e.g. "eng", "eng+kor".</param>
    /// <param name="tessDataPath">Path to tessdata directory. Defaults to "./tessdata".</param>
    public OcrEngine(string language, string? tessDataPath = null)
    {
        tessDataPath ??= Path.Combine(AppContext.BaseDirectory, "tessdata");
        if (!Directory.Exists(tessDataPath))
        {
            tessDataPath = "./tessdata";
        }
        _engine = new TesseractEngine(tessDataPath, language, EngineMode.Default);
    }

    /// <summary>
    /// Recognize text in multiple rectangular regions of an image.
    /// Equivalent to reader.recogss() from Python EasyOCR.
    /// </summary>
    /// <param name="image">The source image as OpenCV Mat.</param>
    /// <param name="rects">List of rectangles [x1, x2, y1, y2].</param>
    /// <returns>List of (rect, recognized text) tuples.</returns>
    public List<(int[] Rect, string Text)> RecognizeRegions(OpenCvSharp.Mat image, List<int[]> rects)
    {
        var results = new List<(int[] Rect, string Text)>();

        // Convert Mat to byte array (BMP)
        byte[] imageBytes = image.ToBytes(".bmp");

        using var pix = Pix.LoadFromMemory(imageBytes);

        foreach (var rect in rects)
        {
            int x1 = rect[0], x2 = rect[1], y1 = rect[2], y2 = rect[3];
            int width = x2 - x1;
            int height = y2 - y1;

            if (width <= 0 || height <= 0) continue;

            // Set region of interest
            _engine.SetVariable("tessedit_pageseg_mode", "7"); // Single text line
            using var page = _engine.Process(pix, new Rect(x1, y1, width, height));
            string text = page.GetText().Trim();

            results.Add((rect, text));
        }

        return results;
    }

    /// <summary>
    /// Recognize text in a single image crop.
    /// </summary>
    public string Recognize(OpenCvSharp.Mat crop)
    {
        byte[] imageBytes = crop.ToBytes(".bmp");
        using var pix = Pix.LoadFromMemory(imageBytes);
        using var page = _engine.Process(pix);
        return page?.GetText()?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Recognize text and return per-line bounding boxes.
    /// </summary>
    public List<TextRegion> RecognizeWithBoxes(OpenCvSharp.Mat crop)
    {
        var results = new List<TextRegion>();
        byte[] imageBytes = crop.ToBytes(".bmp");
        using var pix = Pix.LoadFromMemory(imageBytes);
        using var page = _engine.Process(pix);
        if (page == null) return results;

        string fullText = page.GetText()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(fullText)) return results;

        using var iter = page.GetIterator();
        if (iter == null) return results;

        iter.Begin();
        do
        {
            if (iter.TryGetBoundingBox(PageIteratorLevel.TextLine, out var bounds))
            {
                string text = iter.GetText(PageIteratorLevel.TextLine)?.Trim() ?? "";
                if (!string.IsNullOrWhiteSpace(text))
                {
                    results.Add(new TextRegion(bounds.X1, bounds.Y1, bounds.Width, bounds.Height, text));
                }
            }
        } while (iter.Next(PageIteratorLevel.TextLine));

        return results;
    }

    public void Dispose()
    {
        _engine?.Dispose();
    }
}
