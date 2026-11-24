namespace OneLoaf.Web.Shared.Ui;

/// <summary>An image chosen in <see cref="OlPhotoPicker"/>: its bytes, file name and media type.</summary>
public sealed record PhotoFile(byte[] Content, string FileName, string ContentType);
