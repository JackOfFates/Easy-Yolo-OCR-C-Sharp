using OpenCvSharp;
using System.Text.Json;

namespace EasyYoloOcr.Core;

/// <summary>
/// Data handling: JSON export and masking.
/// Ported from core/data_handler.py.
/// </summary>
public static class DataHandler
{
    /// <summary>
    /// Apply masking to detected regions and save the result.
    /// </summary>
    public static void MaskingSave(ImagePack imagePack, List<Dictionary<string, int>> result, string path)
    {
        string filename = Path.GetFileNameWithoutExtension(path);
        var img = imagePack.OriginalImage.Clone();

        foreach (var rect in result)
        {
            int x1 = rect["x1"], y1 = rect["y1"], x2 = rect["x2"], y2 = rect["y2"];
            img[new Rect(x1, y1, x2 - x1, y2 - y1)].SetTo(new Scalar(0, 0, 0));
        }

        string outputDir = "data/masking";
        Directory.CreateDirectory(outputDir);
        Cv2.ImWrite(Path.Combine(outputDir, $"{filename}.jpg"), img);
    }

    /// <summary>
    /// Create JSON output for masking and OCR results.
    /// </summary>
    public static void MakeJsonMasking(
        List<Dictionary<string, int>> maskingResult,
        List<object> easyAllResult,
        string imgPath,
        int fingerCount)
    {
        var finish = new Dictionary<string, object>
        {
            ["count"] = maskingResult.Count - fingerCount
        };

        if (maskingResult.Count > 0 || easyAllResult.Count > 0)
        {
            finish["masking"] = maskingResult;
            finish["ocr"] = easyAllResult;
        }

        string savePath = imgPath.Replace("link", "json");
        string? folder = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(finish, options);
        File.WriteAllText($"{savePath}.json", json);
    }

    /// <summary>
    /// Print passport scan results.
    /// </summary>
    public static void PrintResultPassport(string fileName, PassportResult? passport)
    {
        if (passport == null)
        {
            Console.WriteLine($"\n\n----- {fileName} Detection Failed -----");
            return;
        }

        Console.WriteLine($"\n\n----- {fileName} Passport Scan Result -----");
        Console.WriteLine($"Type            : {passport.Type}");
        Console.WriteLine($"Issuing county  : {passport.IssuingCountry}");
        Console.WriteLine($"Passport No.    : {passport.PassportNo}");
        Console.WriteLine($"Surname         : {passport.Surname}");
        Console.WriteLine($"Given names     : {passport.GivenNames}");
        Console.WriteLine($"Nationality     : {passport.Nationality}");
        Console.WriteLine($"Date of birth   : {passport.DateOfBirth}");
        Console.WriteLine($"Sex             : {passport.Sex}");
        Console.WriteLine($"Date of expiry  : {passport.DateOfExpiry}");
        Console.WriteLine("---------------------------------------\n");
    }
}

/// <summary>
/// Passport scan result data.
/// </summary>
public class PassportResult
{
    public string Type { get; set; } = "";
    public string IssuingCountry { get; set; } = "";
    public string PassportNo { get; set; } = "";
    public string Surname { get; set; } = "";
    public string GivenNames { get; set; } = "";
    public string Nationality { get; set; } = "";
    public string DateOfBirth { get; set; } = "";
    public string Sex { get; set; } = "";
    public string DateOfExpiry { get; set; } = "";
    public string PersonalNo { get; set; } = "";
}
