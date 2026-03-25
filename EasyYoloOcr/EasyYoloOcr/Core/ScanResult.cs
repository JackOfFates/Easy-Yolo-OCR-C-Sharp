namespace EasyYoloOcr.Core;

/// <summary>
/// Represents a single YOLO detection + OCR result.
/// </summary>
public class ScanResult
{
    /// <summary>YOLO class label.</summary>
    public string Label { get; set; } = "";

    /// <summary>OCR-recognized text.</summary>
    public string Text { get; set; } = "";

    /// <summary>Bounding box as [x1, y1, x2, y2].</summary>
    public float[] BoundingBox { get; set; } = Array.Empty<float>();

    /// <summary>Detection confidence score.</summary>
    public float Confidence { get; set; }
}
