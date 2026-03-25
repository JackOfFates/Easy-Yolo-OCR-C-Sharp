namespace EasyYoloOcr.Core;

/// <summary>
/// Computes intersect ratios, removes overlapping boxes, and provides various utility methods.
/// Ported from core/util.py.
/// </summary>
public static class Util
{
    /// <summary>
    /// Compute the intersection ratio of two rectangles (x1, y1, x2, y2).
    /// </summary>
    public static int ComputeIntersectRatio(float[] rect1, float[] rect2)
    {
        float x1 = rect1[0], y1 = rect1[1], x2 = rect1[2], y2 = rect1[3];
        float x3 = rect2[0], y3 = rect2[1], x4 = rect2[2], y4 = rect2[3];

        if (x2 < x3) return 0;
        if (x1 > x4) return 0;
        if (y2 < y3) return 0;
        if (y1 > y4) return 0;

        float leftUpX = Math.Max(x1, x3);
        float leftUpY = Math.Max(y1, y3);
        float rightDownX = Math.Min(x2, x4);
        float rightDownY = Math.Min(y2, y4);

        float width = rightDownX - leftUpX;
        float height = rightDownY - leftUpY;

        float original = Math.Max((y2 - y1) * (x2 - x1), (y4 - y3) * (x4 - x3));
        float intersect = width * height;

        return (int)(intersect / original * 100);
    }

    /// <summary>
    /// Remove overlapping detection boxes where overlap exceeds ciou threshold.
    /// Keeps the box with higher confidence.
    /// </summary>
    public static List<Detection> UnsortedRemoveIntersectBoxDet(List<Detection> det, float ciou)
    {
        var result = new List<Detection>(det);

        for (int i = 0; i < result.Count - 1; i++)
        {
            if (i > result.Count - 2) break;
            for (int y = i + 1; y < result.Count; y++)
            {
                if (y > result.Count - 1) break;
                if (ComputeIntersectRatio(result[i].Rect, result[y].Rect) > ciou)
                {
                    if (result[i].Confidence > result[y].Confidence)
                    {
                        result.RemoveAt(y);
                        y--;
                    }
                    else
                    {
                        result.RemoveAt(i);
                        i--;
                        break;
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Recursively list all files in a directory.
    /// </summary>
    public static List<string> WatchDir(string path)
    {
        var fileList = new List<string>();
        if (!Directory.Exists(path)) return fileList;

        foreach (var entry in Directory.GetFileSystemEntries(path))
        {
            if (Directory.Exists(entry))
            {
                fileList.AddRange(WatchDir(entry));
            }
            else if (File.Exists(entry))
            {
                fileList.Add(entry);
            }
        }

        return fileList;
    }

    /// <summary>
    /// Crop a region from an image given a rectangle (x1, x2, y1, y2) format used in detection().
    /// </summary>
    public static OpenCvSharp.Mat Crop(int x1, int y1, int x2, int y2, OpenCvSharp.Mat image)
    {
        var rect = new OpenCvSharp.Rect(x1, y1, x2 - x1, y2 - y1);
        return new OpenCvSharp.Mat(image, rect);
    }

    /// <summary>
    /// Returns x evenly divisible by divisor.
    /// </summary>
    public static int MakeDivisible(int x, int divisor)
    {
        return (int)Math.Ceiling((double)x / divisor) * divisor;
    }

    /// <summary>
    /// Verify img_size is a multiple of stride s.
    /// </summary>
    public static int CheckImgSize(int imgSize, int s = 32)
    {
        int newSize = MakeDivisible(imgSize, s);
        if (newSize != imgSize)
        {
            Console.WriteLine($"WARNING: --img-size {imgSize} must be multiple of max stride {s}, updating to {newSize}");
        }
        return newSize;
    }
}
