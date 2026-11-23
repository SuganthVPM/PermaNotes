using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using DesktopNotes.Models;
using DesktopNotes.Views;

namespace DesktopNotes.Services;

public sealed record DocumentSnapshot(string Rtf, string Text, IReadOnlyList<string> Images);

/// <summary>Transforms a detached document only. Never replaces live UI elements during save.</summary>
public sealed class NoteDocumentCodec
{
    public static readonly Regex ImageMarkers = new(@"\[\[PNIMG:[^\]\r\n]+\]\]|⟦IMG:[^⟧\r\n]+⟧", RegexOptions.Compiled);
    private static readonly Regex AllMarkers = new(@"\[\[PNTEXT:(?<literal>Ww==|4p\+m)\]\]|\[\[PNIMG:[^\]\r\n]+\]\]|⟦IMG:[^⟧\r\n]+⟧", RegexOptions.Compiled);
    internal static string EscapeLiteral(string text) => text.Replace("[", "[[PNTEXT:Ww==]]").Replace("⟦", "[[PNTEXT:4p+m]]");
    private static string PlainText(string text) => AllMarkers.Replace(text, match =>
        match.Groups["literal"].Success ? Encoding.UTF8.GetString(Convert.FromBase64String(match.Groups["literal"].Value))
        : NoteImageReference.TryParse(match.Value, out _) ? "[Image]" : match.Value);

    public static string ExtractPlainText(string rtf)
    {
        if (string.IsNullOrEmpty(rtf)) return "";
        var document = new FlowDocument();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(rtf));
        var range = new TextRange(document.ContentStart, document.ContentEnd);
        range.Load(input, System.Windows.DataFormats.Rtf);
        return PlainText(range.Text);
    }

    public DocumentSnapshot Capture(FlowDocument source)
    {
        source.VerifyAccess();
        var copy = Copy(source, out var assets);
        var range = new TextRange(copy.ContentStart, copy.ContentEnd);
        string plain = PlainText(range.Text);
        using var output = new MemoryStream();
        range.Save(output, System.Windows.DataFormats.Rtf);
        return new(Encoding.UTF8.GetString(output.GetBuffer(), 0, checked((int)output.Length)), plain, assets.ToArray());
    }

    public DocumentSnapshot CaptureSelection(FlowDocument source, TextPointer start, TextPointer end)
    {
        var copy = Copy(source, out _);
        int Map(TextPointer pointer)
        {
            int offset = source.ContentStart.GetOffsetToPosition(pointer);
            foreach (var image in Images(source))
                if (image.Parent is InlineUIContainer container && container.ElementEnd.CompareTo(pointer) <= 0)
                    offset += image.Reference.Token.Length - 1;
            foreach (var paragraph in Paragraphs(source.Blocks))
            {
                var cursor = paragraph.ContentStart;
                while (cursor != null && cursor.CompareTo(pointer) < 0 && cursor.CompareTo(paragraph.ContentEnd) < 0)
                {
                    if (cursor.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                    {
                        string text = cursor.GetTextInRun(LogicalDirection.Forward);
                        int length = Math.Min(text.Length, Math.Max(0, cursor.GetOffsetToPosition(pointer)));
                        if (cursor.Parent is not Run { Tag: "PermaNotes.OpaqueImageMarker" })
                            for (int i = 0; i < length; i++) if (text[i] is '[' or '⟦') offset += EscapeLiteral(text[i].ToString()).Length - 1;
                        cursor = cursor.GetPositionAtOffset(text.Length);
                    }
                    else cursor = cursor.GetNextContextPosition(LogicalDirection.Forward);
                }
            }
            return offset;
        }
        var range = new TextRange(copy.ContentStart.GetPositionAtOffset(Map(start))!, copy.ContentStart.GetPositionAtOffset(Map(end))!);
        string plain = PlainText(range.Text);
        using var output = new MemoryStream(); range.Save(output, System.Windows.DataFormats.Rtf);
        var files = Images(source).Where(image => image.Parent is InlineUIContainer container &&
            container.ElementStart.CompareTo(end) < 0 && container.ElementEnd.CompareTo(start) > 0).Select(image => image.FileName).Distinct().ToArray();
        return new(Encoding.UTF8.GetString(output.GetBuffer(), 0, (int)output.Length), plain, files);
    }

    private static FlowDocument Copy(FlowDocument source, out HashSet<string> assets)
    {
        assets = new(StringComparer.OrdinalIgnoreCase);
        var copy = new FlowDocument(); CopyProperties(source, copy);
        copy.FontFamily = source.FontFamily; copy.FontSize = source.FontSize;
        copy.Foreground = source.Foreground; copy.FlowDirection = source.FlowDirection;
        foreach (var block in source.Blocks) copy.Blocks.Add(CloneBlock(block, assets));
        return copy;
    }

    public FlowDocument Load(string rtf, Func<NoteImageReference, NoteImageControl> createImage)
    {
        var document = new FlowDocument();
        if (!string.IsNullOrEmpty(rtf))
        {
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(rtf));
            new TextRange(document.ContentStart, document.ContentEnd).Load(input, System.Windows.DataFormats.Rtf);
        }
        RestoreImages(document, createImage);
        return document;
    }

    public string ExternalRtf(string rtf)
    {
        var document = Load(rtf, _ => new NoteImageControl());
        foreach (var image in Images(document).ToArray())
            if (image.Parent is InlineUIContainer container)
                new TextRange(container.ElementStart, container.ElementEnd).Text = "[Image]";
        using var output = new MemoryStream();
        new TextRange(document.ContentStart, document.ContentEnd).Save(output, System.Windows.DataFormats.Rtf);
        return Encoding.UTF8.GetString(output.GetBuffer(), 0, (int)output.Length);
    }

    public string DuplicateRtf(string rtf)
    {
        var document = Load(rtf, reference =>
        {
            var image = new NoteImageControl();
            image.LoadReference("", reference with { OccurrenceId = Guid.NewGuid() }, 300);
            return image;
        });
        return Capture(document).Rtf;
    }

    public void RestoreImages(FlowDocument document, Func<NoteImageReference, NoteImageControl> createImage,
        TextPointer? limitStart = null, TextPointer? limitEnd = null)
    {
        // Track text runs rather than allocating a WPF TextPointer for every character.
        // Markers can span multiple runs, so keep their offsets in the combined paragraph.
        foreach (var paragraph in Paragraphs(document.Blocks).ToArray())
        {
            var text = new StringBuilder();
            var runs = new List<(int Start, TextPointer Pointer)>();
            var cursor = paragraph.ContentStart;
            while (cursor != null && cursor.CompareTo(paragraph.ContentEnd) < 0)
            {
                if (cursor.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    string run = cursor.GetTextInRun(LogicalDirection.Forward);
                    runs.Add((text.Length, cursor));
                    text.Append(run);
                    cursor = cursor.GetPositionAtOffset(run.Length);
                }
                else
                {
                    if (cursor.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.EmbeddedElement)
                        text.Append('\uFFFC');
                    cursor = cursor.GetNextContextPosition(LogicalDirection.Forward);
                }
            }
            if (runs.Count == 0) continue;
            int runIndex = runs.Count - 1;
            TextPointer PointerAt(int offset)
            {
                while (runs[runIndex].Start > offset) runIndex--;
                var run = runs[runIndex];
                return run.Pointer.GetPositionAtOffset(offset - run.Start)!;
            }

            foreach (Match match in AllMarkers.Matches(text.ToString()).Cast<Match>().Reverse())
            {
                var end = PointerAt(match.Index + match.Length - 1).GetPositionAtOffset(1)!;
                var start = PointerAt(match.Index);
                if (limitStart != null && start.CompareTo(limitStart) < 0 || limitEnd != null && end.CompareTo(limitEnd) > 0) continue;
                if (match.Groups["literal"].Success)
                {
                    new TextRange(start, end).Text = Encoding.UTF8.GetString(Convert.FromBase64String(match.Groups["literal"].Value));
                    continue;
                }
                if (!NoteImageReference.TryParse(match.Value, out var reference))
                {
                    new TextRange(start, end).Text = "";
                    _ = new Run(match.Value, start) { Tag = "PermaNotes.OpaqueImageMarker" };
                    continue;
                }
                new TextRange(start, end).Text = "";
                _ = new InlineUIContainer(createImage(reference), start);
            }
        }
    }

    public static IEnumerable<Paragraph> Paragraphs(BlockCollection blocks)
    {
        foreach (var block in blocks)
        {
            if (block is Paragraph p) yield return p;
            else if (block is Section section)
            { foreach (var item in Paragraphs(section.Blocks)) yield return item; }
            else if (block is System.Windows.Documents.List list)
            { foreach (var li in list.ListItems) foreach (var item in Paragraphs(li.Blocks)) yield return item; }
            else if (block is Table table)
            { foreach (var group in table.RowGroups) foreach (var row in group.Rows) foreach (var cell in row.Cells)
                foreach (var item in Paragraphs(cell.Blocks)) yield return item; }
        }
    }

    public static IEnumerable<NoteImageControl> Images(FlowDocument document)
    {
        foreach (var paragraph in Paragraphs(document.Blocks))
            foreach (var image in Images(paragraph.Inlines)) yield return image;
    }

    private static IEnumerable<NoteImageControl> Images(InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline is InlineUIContainer { Child: NoteImageControl image }) yield return image;
            else if (inline is Span span) foreach (var child in Images(span.Inlines)) yield return child;
        }
    }

    private static Block CloneBlock(Block block, HashSet<string> assets)
    {
        Block copy;
        switch (block)
        {
            case Paragraph paragraph:
                var p = new Paragraph();
                foreach (var inline in paragraph.Inlines) p.Inlines.Add(CloneInline(inline, assets));
                copy = p; break;
            case Section section:
                var s = new Section();
                foreach (var child in section.Blocks) s.Blocks.Add(CloneBlock(child, assets));
                copy = s; break;
            case System.Windows.Documents.List list:
                var l = new System.Windows.Documents.List();
                foreach (var item in list.ListItems)
                {
                    var li = new ListItem(); CopyProperties(item, li);
                    foreach (var child in item.Blocks) li.Blocks.Add(CloneBlock(child, assets));
                    l.ListItems.Add(li);
                }
                copy = l; break;
            case Table table:
                var t = new Table();
                foreach (var column in table.Columns)
                { var c = new TableColumn(); CopyProperties(column, c); t.Columns.Add(c); }
                foreach (var group in table.RowGroups)
                {
                    var g = new TableRowGroup(); CopyProperties(group, g);
                    foreach (var row in group.Rows)
                    {
                        var r = new TableRow(); CopyProperties(row, r);
                        foreach (var cell in row.Cells)
                        {
                            var c = new TableCell(); CopyProperties(cell, c);
                            foreach (var child in cell.Blocks) c.Blocks.Add(CloneBlock(child, assets));
                            r.Cells.Add(c);
                        }
                        g.Rows.Add(r);
                    }
                    t.RowGroups.Add(g);
                }
                copy = t; break;
            default: throw new InvalidDataException($"Cannot save document element {block.GetType().Name}. The previous saved note is unchanged.");
        }
        CopyProperties(block, copy);
        copy.Margin = block.Margin; copy.Padding = block.Padding;
        return copy;
    }

    private static Inline CloneInline(Inline inline, HashSet<string> assets)
    {
        Inline copy;
        if (inline is InlineUIContainer { Child: NoteImageControl image })
        {
            assets.Add(image.FileName);
            copy = new Run(image.Reference.Token);
        }
        else if (inline is Run run) copy = new Run(Equals(run.Tag, "PermaNotes.OpaqueImageMarker") ? run.Text : EscapeLiteral(run.Text));
        else if (inline is LineBreak) copy = new LineBreak();
        else if (inline is Span span)
        {
            Span target = span switch { Hyperlink => new Hyperlink(), Bold => new Bold(), Italic => new Italic(), Underline => new Underline(), _ => new Span() };
            foreach (var child in span.Inlines) target.Inlines.Add(CloneInline(child, assets));
            copy = target;
        }
        else throw new InvalidDataException($"Cannot save inline element {inline.GetType().Name}. The previous saved note is unchanged.");
        CopyProperties(inline, copy);
        return copy;
    }

    private static void CopyProperties(DependencyObject source, DependencyObject target)
    {
        var values = source.GetLocalValueEnumerator();
        while (values.MoveNext())
        {
            var entry = values.Current;
            if (entry.Property.ReadOnly || entry.Property == Run.TextProperty || entry.Value == DependencyProperty.UnsetValue) continue;
            object value = source.GetValue(entry.Property);
            if (value is Freezable freezable) value = freezable.CloneCurrentValue();
            // Resources/styles are editor-owned, not persisted document content.
            if (value is DependencyObject && value is not Freezable) continue;
            target.SetValue(entry.Property, value);
        }
    }
}
