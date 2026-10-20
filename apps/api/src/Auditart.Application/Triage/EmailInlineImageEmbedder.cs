using System.Net;
using System.Text.RegularExpressions;

namespace Auditart.Application.Triage;

/// <summary>
/// Embebe imágenes <c>cid:</c> como data-URI para que el HTML se vea en el portal
/// sin depender del MIME declarado (Outlook a menudo manda octet-stream).
/// </summary>
public static class EmailInlineImageEmbedder
{
    private const int MaxInlineImageBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".tif", ".tiff", ".jfif", ".pjpeg"
    };

    public sealed record ImagePart(string? ContentId, string? FileName, string? ContentType, byte[] Content);

    public static string Embed(string html, IEnumerable<ImagePart> parts)
    {
        if (string.IsNullOrWhiteSpace(html)
            || !html.Contains("cid:", StringComparison.OrdinalIgnoreCase))
            return html;

        var map = new Dictionary<string, (string Mime, byte[] Content)>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in parts)
        {
            if (part.Content.Length == 0 || part.Content.Length > MaxInlineImageBytes)
                continue;

            var mime = ResolveImageMime(part.ContentType, part.FileName, part.Content);
            if (!mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                // Content-ID + octet-stream / sin MIME: firmas Outlook suelen ser jpeg sin etiquetar.
                var allowJpegFallback = !string.IsNullOrWhiteSpace(part.ContentId)
                    && (LooksLikeImage(part.ContentType, part.FileName)
                        || string.IsNullOrWhiteSpace(part.ContentType)
                        || part.ContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase));
                if (!allowJpegFallback)
                    continue;
                mime = "image/jpeg";
            }

            foreach (var key in CidKeys(part.ContentId, part.FileName))
                map[key] = (mime, part.Content);
        }

        if (map.Count == 0) return html;

        return Regex.Replace(
            html,
            @"cid:([^""'\s>)]+)",
            match =>
            {
                var raw = match.Groups[1].Value.Trim().Trim('<', '>', '"', '\'');
                var resolved = Resolve(map, raw);
                if (resolved is null) return match.Value;
                var (mime, content) = resolved.Value;
                return $"data:{mime};base64,{Convert.ToBase64String(content)}";
            },
            RegexOptions.IgnoreCase);
    }

    private static (string Mime, byte[] Content)? Resolve(
        Dictionary<string, (string Mime, byte[] Content)> map,
        string rawCid)
    {
        foreach (var key in CidKeys(rawCid, rawCid))
        {
            if (map.TryGetValue(key, out var image))
                return image;
        }

        var decoded = WebUtility.UrlDecode(rawCid)?.Trim().Trim('<', '>') ?? rawCid;
        var local = decoded.Split('@')[0];
        if (local.Length < 3) return null;

        foreach (var (key, image) in map)
        {
            if (key.Contains(local, StringComparison.OrdinalIgnoreCase)
                || local.Contains(key, StringComparison.OrdinalIgnoreCase))
                return image;
        }

        return null;
    }

    private static IEnumerable<string> CidKeys(string? contentId, string? fileName)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            var v = WebUtility.UrlDecode(value.Trim())?.Trim().Trim('<', '>', '"', '\'') ?? string.Empty;
            if (v.Length == 0) return;
            keys.Add(v);
            var at = v.IndexOf('@');
            if (at > 0) keys.Add(v[..at]);
            var slash = Math.Max(v.LastIndexOf('/'), v.LastIndexOf('\\'));
            if (slash >= 0 && slash < v.Length - 1) keys.Add(v[(slash + 1)..]);
        }

        Add(contentId);
        Add(fileName);
        if (!string.IsNullOrWhiteSpace(fileName))
            Add(Path.GetFileName(fileName));
        return keys;
    }

    public static bool LooksLikeImage(string? mimeType, string? fileName)
    {
        if (!string.IsNullOrWhiteSpace(mimeType)
            && mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.IsNullOrWhiteSpace(fileName)) return false;
        var ext = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(ext) && ImageExtensions.Contains(ext);
    }

    public static string ResolveImageMime(string? declared, string? fileName, byte[] content)
    {
        var sniffed = SniffImageMime(content);
        if (sniffed is not null) return sniffed;

        if (!string.IsNullOrWhiteSpace(declared)
            && declared.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            if (declared.Equals("image/jpg", StringComparison.OrdinalIgnoreCase)
                || declared.Equals("image/pjpeg", StringComparison.OrdinalIgnoreCase))
                return "image/jpeg";
            return declared;
        }

        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" or ".jfif" or ".pjpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => declared is { Length: > 0 } ? declared : "application/octet-stream"
        };
    }

    private static string? SniffImageMime(byte[] content)
    {
        if (content.Length < 12) return null;
        if (content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47)
            return "image/png";
        if (content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
            return "image/jpeg";
        if (content[0] == (byte)'G' && content[1] == (byte)'I' && content[2] == (byte)'F')
            return "image/gif";
        if (content[0] == (byte)'R' && content[1] == (byte)'I' && content[2] == (byte)'F' && content[3] == (byte)'F'
            && content[8] == (byte)'W' && content[9] == (byte)'E' && content[10] == (byte)'B' && content[11] == (byte)'P')
            return "image/webp";
        if (content[0] == (byte)'B' && content[1] == (byte)'M')
            return "image/bmp";
        return null;
    }
}
