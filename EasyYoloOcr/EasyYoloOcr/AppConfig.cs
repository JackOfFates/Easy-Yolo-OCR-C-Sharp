using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EasyYoloOcr;

public class AppConfig
{
    [YamlMember(Alias = "images")]
    public string Images { get; set; } = "image";

    [YamlMember(Alias = "detection")]
    public string Detection { get; set; } = "weights/example.onnx";

    [YamlMember(Alias = "detection-size")]
    public int DetectionSize { get; set; } = 640;

    [YamlMember(Alias = "detection-confidence")]
    public float DetectionConfidence { get; set; } = 0.25f;

    [YamlMember(Alias = "detection-iou")]
    public float DetectionIou { get; set; } = 0.25f;

    public static AppConfig Load(string path)
    {
        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance)
            .Build();
        return deserializer.Deserialize<AppConfig>(yaml);
    }
}
