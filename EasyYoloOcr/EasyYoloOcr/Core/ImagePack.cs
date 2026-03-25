using OpenCvSharp;

namespace EasyYoloOcr.Core;

/// <summary>
/// Handles image loading, preprocessing, cropping, and resizing.
/// Ported from core/image_handler.py.
/// </summary>
public class ImagePack
{
    /// <summary>Original image.</summary>
    public Mat OriginalImage { get; private set; }

    /// <summary>Current working image.</summary>
    public Mat CurrentImage { get; private set; }

    public int ImgSize { get; private set; }
    public int Stride { get; private set; }

    public ImagePack(string path, int imgSize = 640, int stride = 32, bool byteMode = false, bool gray = false)
    {
        if (byteMode)
        {
            OriginalImage = Cv2.ImRead(path);
        }
        else
        {
            if (path.ToLower().EndsWith(".gif"))
            {
                using var capture = new VideoCapture(path);
                var frame = new Mat();
                if (capture.Read(frame))
                {
                    OriginalImage = frame;
                }
                else
                {
                    throw new InvalidOperationException($"Cannot read GIF: {path}");
                }
            }
            else
            {
                // Load via byte buffer for Unicode path support (matches Python np.fromfile + imdecode)
                byte[] fileBytes = File.ReadAllBytes(path);
                OriginalImage = Cv2.ImDecode(fileBytes, ImreadModes.Color);
            }
        }

        if (OriginalImage == null || OriginalImage.Empty())
            throw new FileNotFoundException($"Image not found: {path}");

        if (gray)
        {
            Cv2.CvtColor(OriginalImage, OriginalImage, ColorConversionCodes.BGR2GRAY);
            Cv2.CvtColor(OriginalImage, OriginalImage, ColorConversionCodes.GRAY2BGR);
        }

        if (OriginalImage.Width < 1280)
        {
            OriginalImage = ResizeKeepRatio(OriginalImage, 1280);
        }

        CurrentImage = OriginalImage.Clone();
        ImgSize = imgSize;
        Stride = stride;
    }

    public ImagePack(Mat image, int imgSize = 640, int stride = 32, bool gray = false)
    {
        OriginalImage = image.Clone();

        if (gray)
        {
            Cv2.CvtColor(OriginalImage, OriginalImage, ColorConversionCodes.BGR2GRAY);
            Cv2.CvtColor(OriginalImage, OriginalImage, ColorConversionCodes.GRAY2BGR);
        }

        if (OriginalImage.Width < 1280)
        {
            OriginalImage = ResizeKeepRatio(OriginalImage, 1280);
        }

        CurrentImage = OriginalImage.Clone();
        ImgSize = imgSize;
        Stride = stride;
    }

    /// <summary>
    /// Crop a rectangular region from an image.
    /// </summary>
    public static Mat CropRegion(int x1, int y1, int x2, int y2, Mat image)
    {
        var rect = new Rect(x1, y1, x2 - x1, y2 - y1);
        return new Mat(image, rect);
    }

    /// <summary>
    /// Resize image while keeping aspect ratio.
    /// </summary>
    public static Mat ResizeKeepRatio(Mat image, int targetSize)
    {
        int width = image.Width;
        int height = image.Height;
        bool widthBetter = width > height;

        double ratio;
        Size newSize;

        if (widthBetter)
        {
            ratio = (double)targetSize / width;
            newSize = new Size(targetSize, (int)(height * ratio));
        }
        else
        {
            ratio = (double)targetSize / height;
            newSize = new Size((int)(width * ratio), targetSize);
        }

        var resized = new Mat();
        Cv2.Resize(image, resized, newSize);
        return resized;
    }

    /// <summary>
    /// Set the current image to a cropped region.
    /// </summary>
    public void SetCrop(int x1, int y1, int x2, int y2)
    {
        CurrentImage = CropRegion(x1, y1, x2, y2, CurrentImage);
    }

    /// <summary>
    /// Reset to original image.
    /// </summary>
    public void Reset()
    {
        CurrentImage = OriginalImage.Clone();
    }

    /// <summary>
    /// Make the image square by padding with gray.
    /// </summary>
    public void MakeSquareWithGray()
    {
        int w = CurrentImage.Width;
        int h = CurrentImage.Height;

        if (w > h)
        {
            var gray = new Mat(w - h, w, MatType.CV_8UC3, new Scalar(178, 178, 178));
            Cv2.VConcat(CurrentImage, gray, CurrentImage);
        }
        else
        {
            var gray = new Mat(h, h - w, MatType.CV_8UC3, new Scalar(178, 178, 178));
            Cv2.HConcat(CurrentImage, gray, CurrentImage);
        }
    }
}
