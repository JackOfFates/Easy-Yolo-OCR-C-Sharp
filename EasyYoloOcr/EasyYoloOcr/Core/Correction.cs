namespace EasyYoloOcr.Core;

/// <summary>
/// Text correction utilities for OCR output.
/// Ported from core/correction.py.
/// </summary>
public static class Correction
{
    /// <summary>
    /// Correct nationality code by matching against known nationality codes.
    /// </summary>
    public static string NationCorrection(string value, string nationalityFilePath = "res/nationality.txt")
    {
        if (!File.Exists(nationalityFilePath))
            return value;

        var nationalities = File.ReadAllLines(nationalityFilePath)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();

        if (value.Length != 3) return value;

        // Exact match
        if (nationalities.Contains(value)) return value;

        string strFront = value[..2];
        string strBack = value[1..];
        string strMiddle = $"{value[0]}{value[2]}";

        if (strFront == "KO") return "KOR";
        if (strBack == "OR") return "KOR";
        if (strMiddle == "KR") return "KOR";

        // Match first two characters
        var frontMatches = nationalities.Where(n => n.Length == 3 && n[..2] == strFront).ToList();
        if (frontMatches.Count == 1) return frontMatches[0];

        // Match last two characters
        var backMatches = nationalities.Where(n => n.Length == 3 && n[1..] == strBack).ToList();
        if (backMatches.Count == 1) return backMatches[0];

        // Match first and third character
        var middleMatches = nationalities
            .Where(n => n.Length == 3 && $"{n[0]}{n[2]}" == strMiddle).ToList();
        if (middleMatches.Count == 1) return middleMatches[0];

        return value;
    }

    /// <summary>
    /// Correct MRZ characters between English letters and digits.
    /// </summary>
    /// <param name="value">Input string.</param>
    /// <param name="flag">"en2dg" to convert letters to digits, anything else for digits to letters.</param>
    public static string MrzCorrection(string value, string flag)
    {
        if (flag == "en2dg")
        {
            return value
                .Replace('O', '0').Replace('Q', '0').Replace('U', '0').Replace('D', '0')
                .Replace('I', '1').Replace('Z', '2').Replace('B', '3').Replace('A', '4').Replace('S', '5');
        }
        else
        {
            return value
                .Replace('0', 'O').Replace('1', 'I').Replace('2', 'Z').Replace('3', 'B')
                .Replace('4', 'A').Replace('8', 'B');
        }
    }

    /// <summary>
    /// Correct sex/gender character.
    /// </summary>
    public static string SexCorrection(string value)
    {
        return value.Replace('P', 'F').Replace('T', 'F').Replace('N', 'M');
    }

    /// <summary>
    /// Correct passport type.
    /// </summary>
    public static string TypeCorrection(string value)
    {
        return value.Replace("FM", "PM").Replace("PN", "PM");
    }
}
