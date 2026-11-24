using System.Text;

namespace OneLoaf.Web.Features.Assistant.Services;

/// <summary>A piece of inline text of an assistant reply.</summary>
/// <param name="Text">Text as written (never HTML).</param>
/// <param name="Bold">Strong emphasis.</param>
/// <param name="Italic">Emphasis.</param>
/// <param name="Code">Inline code.</param>
/// <param name="Href">Link target; only http and https links are kept.</param>
public sealed record MarkdownInline(string Text, bool Bold = false, bool Italic = false, bool Code = false, string? Href = null);

/// <summary>Kind of a block of an assistant reply.</summary>
public enum MarkdownBlockKind
{
    /// <summary>Paragraph.</summary>
    Paragraph,

    /// <summary>Heading.</summary>
    Heading,

    /// <summary>Bulleted list.</summary>
    BulletList,

    /// <summary>Numbered list.</summary>
    NumberedList,

    /// <summary>Fenced code block.</summary>
    Code,
}

/// <summary>A block of an assistant reply: its inline content, or one inline sequence per list item.</summary>
/// <param name="Kind">Kind.</param>
/// <param name="Inlines">Content of a paragraph or heading; the text of a code block.</param>
/// <param name="Items">Items of a list.</param>
/// <param name="Level">Heading level 1-3.</param>
public sealed record MarkdownBlock(
    MarkdownBlockKind Kind, IReadOnlyList<MarkdownInline> Inlines, IReadOnlyList<IReadOnlyList<MarkdownInline>>? Items = null, int Level = 0);

/// <summary>
/// Reads the small part of Markdown the assistant writes: paragraphs, headings, bulleted and numbered lists, fenced
/// code, bold, italic, inline code and links. The result is a tree of plain text pieces that the page renders as
/// elements, so nothing the model writes is ever interpreted as HTML.
/// </summary>
public static class MarkdownParser
{
    private const int MaxHeadingLevel = 3;

    /// <summary>Parses a reply into blocks.</summary>
    public static IReadOnlyList<MarkdownBlock> Parse(string? text)
    {
        var blocks = new List<MarkdownBlock>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return blocks;
        }

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var paragraph = new List<string>();
        List<IReadOnlyList<MarkdownInline>>? list = null;
        var listKind = MarkdownBlockKind.BulletList;

        void FlushParagraph()
        {
            if (paragraph.Count > 0)
            {
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Paragraph, ParseInline(string.Join(' ', paragraph))));
                paragraph.Clear();
            }
        }

        void FlushList()
        {
            if (list is not null)
            {
                blocks.Add(new MarkdownBlock(listKind, [], list));
                list = null;
            }
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd();
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph();
                FlushList();
                var code = new StringBuilder();
                for (i++; i < lines.Length && !lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal); i++)
                {
                    code.Append(lines[i].TrimEnd('\r')).Append('\n');
                }

                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Code, [new MarkdownInline(code.ToString().TrimEnd('\n'), Code: true)]));
                continue;
            }

            if (trimmed.Length == 0)
            {
                FlushParagraph();
                FlushList();
                continue;
            }

            if (HeadingLevel(trimmed) is var level and > 0)
            {
                FlushParagraph();
                FlushList();
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Heading, ParseInline(trimmed[(level + 1)..].Trim()), Level: Math.Min(level, MaxHeadingLevel)));
                continue;
            }

            if (ListItem(trimmed, out var kind) is { } item)
            {
                FlushParagraph();
                if (list is not null && kind != listKind)
                {
                    FlushList();
                }

                list ??= [];
                listKind = kind;
                list.Add(ParseInline(item));
                continue;
            }

            // A line that continues a list item is indented; anything else starts a paragraph.
            if (list is not null && line.Length > trimmed.Length)
            {
                list[^1] = [.. list[^1], new MarkdownInline(" "), .. ParseInline(trimmed)];
                continue;
            }

            FlushList();
            paragraph.Add(trimmed);
        }

        FlushParagraph();
        FlushList();
        return blocks;
    }

    /// <summary>Parses bold, italic, inline code and links of one line.</summary>
    public static IReadOnlyList<MarkdownInline> ParseInline(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new List<MarkdownInline>();
        var plain = new StringBuilder();
        var bold = false;
        var italic = false;

        void Flush()
        {
            if (plain.Length > 0)
            {
                result.Add(new MarkdownInline(plain.ToString(), bold, italic));
                plain.Clear();
            }
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '`' && text.IndexOf('`', i + 1) is var end and > 0)
            {
                Flush();
                result.Add(new MarkdownInline(text[(i + 1)..end], bold, italic, Code: true));
                i = end;
            }
            else if (c == '[' && TryLink(text, i, out var label, out var href, out var next))
            {
                Flush();
                result.Add(new MarkdownInline(label, bold, italic, Href: href));
                i = next;
            }
            else if (IsMarker(text, i, 2) && (bold || text.IndexOf(text.Substring(i, 2), i + 2, StringComparison.Ordinal) > 0))
            {
                Flush();
                bold = !bold;
                i++;
            }
            else if (IsMarker(text, i, 1) && IsEmphasisBoundary(text, i, italic))
            {
                Flush();
                italic = !italic;
            }
            else
            {
                plain.Append(c);
            }
        }

        Flush();
        return result;
    }

    private static int HeadingLevel(string line)
    {
        var level = 0;
        while (level < line.Length && line[level] == '#')
        {
            level++;
        }

        return level is > 0 and <= 6 && level < line.Length && line[level] == ' ' ? level : 0;
    }

    private static string? ListItem(string line, out MarkdownBlockKind kind)
    {
        kind = MarkdownBlockKind.BulletList;
        if (line.Length > 2 && line[0] is '-' or '*' or '•' && line[1] == ' ')
        {
            return line[2..].Trim();
        }

        var digits = 0;
        while (digits < line.Length && char.IsAsciiDigit(line[digits]))
        {
            digits++;
        }

        if (digits is > 0 and < 4 && digits + 1 < line.Length && line[digits] is '.' or ')' && line[digits + 1] == ' ')
        {
            kind = MarkdownBlockKind.NumberedList;
            return line[(digits + 2)..].Trim();
        }

        return null;
    }

    private static bool IsMarker(string text, int index, int length)
    {
        if (index + length > text.Length || text[index] is not ('*' or '_'))
        {
            return false;
        }

        for (var i = 1; i < length; i++)
        {
            if (text[index + i] != text[index])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// A single * or _ opens emphasis only before a word and when it is closed later; it closes emphasis only after
    /// a word. So "2 * 3" and "snake_case" stay as written.
    /// </summary>
    private static bool IsEmphasisBoundary(string text, int index, bool open)
    {
        var marker = text[index];
        if (open)
        {
            return index > 0 && !char.IsWhiteSpace(text[index - 1])
                && (marker == '*' || index + 1 == text.Length || !char.IsLetterOrDigit(text[index + 1]));
        }

        var startsWord = index + 1 < text.Length && !char.IsWhiteSpace(text[index + 1])
            && (marker == '*' || index == 0 || !char.IsLetterOrDigit(text[index - 1]));
        return startsWord && text.IndexOf(marker, index + 1) > 0;
    }

    private static bool TryLink(string text, int start, out string label, out string href, out int end)
    {
        label = href = string.Empty;
        end = start;
        var close = text.IndexOf(']', start + 1);
        if (close < 0 || close + 1 >= text.Length || text[close + 1] != '(')
        {
            return false;
        }

        var urlEnd = text.IndexOf(')', close + 2);
        if (urlEnd < 0)
        {
            return false;
        }

        var url = text[(close + 2)..urlEnd].Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        label = text[(start + 1)..close];
        href = uri.AbsoluteUri;
        end = urlEnd;
        return label.Length > 0;
    }
}
