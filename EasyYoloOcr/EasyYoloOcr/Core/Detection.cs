namespace EasyYoloOcr.Core;

/// <summary>
/// Represents a single detection result with bounding box, confidence, and class.
/// </summary>
public class Detection
{
    /// <summary>
    /// Bounding box as [x1, y1, x2, y2].
    /// </summary>
    public float[] Rect { get; set; } = new float[4];

    /// <summary>
    /// Detection confidence score.
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// Class index.
    /// </summary>
    public int ClassId { get; set; }

    public Detection() { }

    public Detection(float[] rect, float confidence, int classId)
    {
        Rect = rect;
        Confidence = confidence;
        ClassId = classId;
    }
}
