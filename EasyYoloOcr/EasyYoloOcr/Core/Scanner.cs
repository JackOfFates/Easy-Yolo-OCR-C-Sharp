using OpenCvSharp;

namespace EasyYoloOcr.Core;

/// <summary>
/// Main scanning/detection pipeline.
/// Ported from core/scan.py.
/// </summary>
public static class Scanner
{
    /// <summary>
    /// Run YOLO detection + OCR on an image file.
    /// </summary>
    public static List<ScanResult> PtDetect(
        string path,
        YoloDetector detector,
        float ciou,
        OcrEngine ocrEngine,
        int imgSize,
        float confidence,
        float iou,
        bool gray = false,
        bool byteMode = false)
    {
        var imagePack = new ImagePack(path, imgSize, gray: gray, byteMode: byteMode);
        return RunDetection(imagePack, detector, ciou, ocrEngine, imgSize, confidence, iou);
    }

    /// <summary>
    /// Run YOLO detection + OCR on a Mat image.
    /// </summary>
    public static List<ScanResult> PtDetect(
        Mat image,
        YoloDetector detector,
        float ciou,
        OcrEngine ocrEngine,
        int imgSize,
        float confidence,
        float iou,
        bool gray = false)
    {
        var imagePack = new ImagePack(image, imgSize, gray: gray);
        return RunDetection(imagePack, detector, ciou, ocrEngine, imgSize, confidence, iou);
    }

    private static List<ScanResult> RunDetection(
        ImagePack imagePack,
        YoloDetector detector,
        float ciou,
        OcrEngine ocrEngine,
        int imgSize,
        float confidence,
        float iou)
    {
        int checkedImgSize = Util.CheckImgSize(imgSize);

        // Run YOLO detection
        var detections = detector.Detect(imagePack.CurrentImage, checkedImgSize, confidence, iou, ciou);

        // Sort detections by class ID (matches Python: det.sort(key=lambda x: x[2]))
        detections.Sort((a, b) => a.ClassId.CompareTo(b.ClassId));

        // Extract rectangles and labels
        var rectList = new List<int[]>();
        var labelList = new List<string>();

        foreach (var det in detections)
        {
            labelList.Add(detector.Names.Length > det.ClassId
                ? detector.Names[det.ClassId]
                : $"class{det.ClassId}");

            // Format: [x1, x2, y1, y2] for OCR (matches Python detection() output)
            rectList.Add(new[]
            {
                (int)det.Rect[0], // x1
                (int)det.Rect[2], // x2
                (int)det.Rect[1], // y1
                (int)det.Rect[3]  // y2
            });
        }

        // Run OCR on detected regions
        var ocrResults = ocrEngine.RecognizeRegions(imagePack.CurrentImage, rectList);

        // Build structured results
        var results = new List<ScanResult>();
        for (int i = 0; i < ocrResults.Count; i++)
        {
            string label = i < labelList.Count ? labelList[i] : "unknown";
            var det = i < detections.Count ? detections[i] : null;
            results.Add(new ScanResult
            {
                Label = label,
                Text = ocrResults[i].Text,
                BoundingBox = det?.Rect ?? Array.Empty<float>(),
                Confidence = det?.Confidence ?? 0
            });
        }

        return results;
    }
}
