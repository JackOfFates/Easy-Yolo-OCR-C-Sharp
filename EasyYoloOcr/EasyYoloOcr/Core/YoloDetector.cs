using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace EasyYoloOcr.Core;

/// <summary>
/// YOLO detector using ONNX Runtime.
/// Ported from core/general.py and core/scan.py (model_setting, detecting).
/// 
/// NOTE: The original Python project uses PyTorch .pt weights. For C#, you must
/// first export your model to ONNX format using:
///   python yolov5/export.py --weights weights/example.pt --include onnx
/// Then reference the .onnx file in config.yaml.
/// </summary>
public class YoloDetector : IDisposable
{
    private readonly InferenceSession _session;
    private readonly string _inputName;

    public string[] Names { get; }

    public YoloDetector(string modelPath, int gpuDeviceId = -1)
    {
        var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions();

        if (gpuDeviceId >= 0)
        {
            sessionOptions.AppendExecutionProvider_CUDA(gpuDeviceId);
        }

        _session = new InferenceSession(modelPath, sessionOptions);
        _inputName = _session.InputMetadata.Keys.First();

        // Try to extract class names from metadata; fall back to numeric names
        if (_session.ModelMetadata.CustomMetadataMap.TryGetValue("names", out var namesJson))
        {
            // YOLOv5 ONNX export stores names as "{0: 'class0', 1: 'class1', ...}"
            Names = ParseClassNames(namesJson);
        }
        else
        {
            var outputShape = _session.OutputMetadata.Values.First().Dimensions;
            int numClasses = outputShape.Last() - 5; // (x, y, w, h, obj_conf, cls0, cls1, ...)
            Names = Enumerable.Range(0, Math.Max(numClasses, 1)).Select(i => $"class{i}").ToArray();
        }
    }

    /// <summary>
    /// Run inference and return raw detections after NMS and duplicate box removal.
    /// </summary>
    public List<Detection> Detect(Mat image, int imgSize, float confThreshold, float iouThreshold, float ciou)
    {
        // Preprocess: letterbox resize
        var (inputTensor, ratioW, ratioH, padW, padH) = Preprocess(image, imgSize);

        // Run inference
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
        };

        using var results = _session.Run(inputs);
        var output = results.First().AsTensor<float>();

        // Post-process: extract detections
        var rawDetections = PostProcess(output, confThreshold);

        // Apply NMS
        var nmsDetections = NonMaxSuppression(rawDetections, iouThreshold);

        // Scale coordinates back to original image
        foreach (var det in nmsDetections)
        {
            det.Rect[0] = (det.Rect[0] - padW) / ratioW;
            det.Rect[1] = (det.Rect[1] - padH) / ratioH;
            det.Rect[2] = (det.Rect[2] - padW) / ratioW;
            det.Rect[3] = (det.Rect[3] - padH) / ratioH;

            // Clip to image bounds
            det.Rect[0] = Math.Max(0, Math.Min(det.Rect[0], image.Width));
            det.Rect[1] = Math.Max(0, Math.Min(det.Rect[1], image.Height));
            det.Rect[2] = Math.Max(0, Math.Min(det.Rect[2], image.Width));
            det.Rect[3] = Math.Max(0, Math.Min(det.Rect[3], image.Height));
        }

        // Remove duplicate overlapping boxes
        return Util.UnsortedRemoveIntersectBoxDet(nmsDetections, ciou);
    }

    private (DenseTensor<float> tensor, float ratioW, float ratioH, float padW, float padH)
        Preprocess(Mat image, int imgSize)
    {
        int origH = image.Height;
        int origW = image.Width;

        float ratio = Math.Min((float)imgSize / origH, (float)imgSize / origW);
        int newW = (int)Math.Round(origW * ratio);
        int newH = (int)Math.Round(origH * ratio);

        float padW = (imgSize - newW) / 2.0f;
        float padH = (imgSize - newH) / 2.0f;

        using var resized = new Mat();
        Cv2.Resize(image, resized, new Size(newW, newH), interpolation: InterpolationFlags.Linear);

        int top = (int)Math.Round(padH - 0.1);
        int bottom = (int)Math.Round(padH + 0.1);
        int left = (int)Math.Round(padW - 0.1);
        int right = (int)Math.Round(padW + 0.1);

        using var padded = new Mat();
        Cv2.CopyMakeBorder(resized, padded, top, bottom, left, right,
            BorderTypes.Constant, new Scalar(114, 114, 114));

        // Convert BGR -> RGB, normalize to [0,1], create tensor [1, 3, H, W]
        var tensor = new DenseTensor<float>(new[] { 1, 3, imgSize, imgSize });

        var indexer = padded.GetGenericIndexer<Vec3b>();
        for (int y = 0; y < imgSize; y++)
        {
            for (int x = 0; x < imgSize; x++)
            {
                Vec3b pixel = indexer[y, x];
                tensor[0, 0, y, x] = pixel[2] / 255.0f; // R
                tensor[0, 1, y, x] = pixel[1] / 255.0f; // G
                tensor[0, 2, y, x] = pixel[0] / 255.0f; // B
            }
        }

        float ratioW = ratio;
        float ratioH = ratio;

        return (tensor, ratioW, ratioH, padW, padH);
    }

    private List<Detection> PostProcess(Tensor<float> output, float confThreshold)
    {
        var detections = new List<Detection>();
        int numDetections = output.Dimensions[1];
        int numAttributes = output.Dimensions[2]; // x, y, w, h, obj_conf, cls0, cls1, ...
        int numClasses = numAttributes - 5;

        for (int i = 0; i < numDetections; i++)
        {
            float objConf = output[0, i, 4];
            if (objConf < confThreshold) continue;

            // Find best class
            int bestClass = 0;
            float bestScore = 0;
            for (int c = 0; c < numClasses; c++)
            {
                float score = output[0, i, 5 + c] * objConf;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestClass = c;
                }
            }

            if (bestScore < confThreshold) continue;

            // Convert xywh -> xyxy
            float cx = output[0, i, 0];
            float cy = output[0, i, 1];
            float w = output[0, i, 2];
            float h = output[0, i, 3];

            float x1 = cx - w / 2;
            float y1 = cy - h / 2;
            float x2 = cx + w / 2;
            float y2 = cy + h / 2;

            detections.Add(new Detection(
                new[] { x1, y1, x2, y2 },
                bestScore,
                bestClass));
        }

        return detections;
    }

    /// <summary>
    /// Non-Maximum Suppression.
    /// </summary>
    private static List<Detection> NonMaxSuppression(List<Detection> detections, float iouThreshold)
    {
        var result = new List<Detection>();

        // Group by class
        var groups = detections.GroupBy(d => d.ClassId);
        foreach (var group in groups)
        {
            var sorted = group.OrderByDescending(d => d.Confidence).ToList();
            while (sorted.Count > 0)
            {
                var best = sorted[0];
                result.Add(best);
                sorted.RemoveAt(0);

                sorted.RemoveAll(d => ComputeIoU(best.Rect, d.Rect) > iouThreshold);
            }
        }

        return result;
    }

    private static float ComputeIoU(float[] a, float[] b)
    {
        float x1 = Math.Max(a[0], b[0]);
        float y1 = Math.Max(a[1], b[1]);
        float x2 = Math.Min(a[2], b[2]);
        float y2 = Math.Min(a[3], b[3]);

        float interArea = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        float areaA = (a[2] - a[0]) * (a[3] - a[1]);
        float areaB = (b[2] - b[0]) * (b[3] - b[1]);

        return interArea / (areaA + areaB - interArea);
    }

    private static string[] ParseClassNames(string namesJson)
    {
        // Parse "{0: 'class0', 1: 'class1', ...}" format
        var names = new List<string>();
        var parts = namesJson.Trim('{', '}').Split(',');
        foreach (var part in parts)
        {
            var kv = part.Split(':');
            if (kv.Length == 2)
            {
                names.Add(kv[1].Trim().Trim('\'', '"', ' '));
            }
        }
        return names.ToArray();
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
