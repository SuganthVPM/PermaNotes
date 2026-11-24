using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace DesktopNotes.Models;

/// <summary>An occurrence in a document; the immutable asset can occur more than once.</summary>
public sealed record NoteImageReference(string FileName, double WidthRatio, Guid OccurrenceId)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Token => "[[PNIMG:1:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(this))) + "]]";

    public static bool IsSafeFileName(string name) => !string.IsNullOrWhiteSpace(name)
        && name == Path.GetFileName(name) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
        && !name.Contains("..") && !Path.IsPathRooted(name);

    public static bool TryParse(string token, out NoteImageReference reference)
    {
        reference = null!;
        try
        {
            if (token.StartsWith("[[PNIMG:1:", StringComparison.Ordinal) && token.EndsWith("]]", StringComparison.Ordinal))
            {
                var parsed = JsonSerializer.Deserialize<NoteImageReference>(
                    Encoding.UTF8.GetString(Convert.FromBase64String(token[10..^2])));
                if (parsed == null || !IsSafeFileName(parsed.FileName) || !double.IsFinite(parsed.WidthRatio)) return false;
                reference = parsed with { WidthRatio = Math.Clamp(parsed.WidthRatio, .15, 1) };
                return true;
            }
            if (token.StartsWith("⟦IMG:", StringComparison.Ordinal) && token.EndsWith('⟧'))
            {
                var parts = token[5..^1].Split("|r=", StringSplitOptions.None);
                if (!IsSafeFileName(parts[0])) return false;
                double width = .3;
                if (parts.Length > 1 && (!double.TryParse(parts[1].Replace(',', '.'), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out width) || !double.IsFinite(width))) return false;
                reference = new(parts[0], Math.Clamp(width, .15, 1), Guid.NewGuid());
                return true;
            }
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException) { }
        return false;
    }
}
