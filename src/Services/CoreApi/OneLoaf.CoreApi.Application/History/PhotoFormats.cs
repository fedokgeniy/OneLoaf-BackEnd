namespace OneLoaf.CoreApi.Application.History;

/// <summary>Limits of photo uploads.</summary>
public static class PhotoLimits
{
    /// <summary>Largest file.</summary>
    public const long MaxBytes = 5 * 1024 * 1024;

    /// <summary>Most photos per bake record.</summary>
    public const int MaxPerRecord = 10;

    /// <summary>Form field of the file.</summary>
    public const string FormField = "file";
}

/// <summary>Image formats accepted for photos, recognised by their file signature (not by the declared type).</summary>
public static class PhotoFormats
{
    /// <summary>Bytes needed to recognise a format.</summary>
    public const int SignatureLength = 12;

    /// <summary>Media type and file extension of the image, or null for anything else.</summary>
    public static (string ContentType, string Extension)? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return ("image/jpeg", "jpg");
        }

        if (header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return ("image/png", "png");
        }

        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
        {
            return ("image/webp", "webp");
        }

        return null;
    }
}
