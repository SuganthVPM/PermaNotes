using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopNotes.Models;
using DesktopNotes.Services;
using DesktopNotes.Views;

internal static class Program
{
    private static int _checks;
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "fixtures", Guid.NewGuid().ToString("N"));
    [STAThread]
    private static int Main(string[] args)
    {
        Directory.CreateDirectory(Root);
        if (args.Length == 1 && args[0] == "--memory-notes") { BenchmarkNotes(); return 0; }
        if (args.Length == 1 && args[0] == "--memory-breakdown") { BenchmarkMemoryBreakdown(); return 0; }
        if (args.Length == 3 && args[0] == "--memory-stage") { MeasureMemoryStage(args[1], args[2]); return 0; }
        if (args.Length == 1 && args[0] == "--preview") { RenderPreview(); return 0; }
        if (args.Length == 2 && args[0].StartsWith("--memory-", StringComparison.Ordinal)) return MemoryBenchmark(args[0] == "--memory-full", args[1]);
        try
        {
            TokenTests(); CodecTests(); AssetTests(); StorageTests(); HistoryTests(); MigrationTests(); CollectionTests(); SessionTests(); ThumbnailLifetimeTest(); RenderPreview(); MemoryComparisons();
            Console.WriteLine($"PASS: {_checks} checks. Isolated fixtures: {Root}");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); _checks++; Console.WriteLine("PASS " + message); }
    private static void Throws(Action action, string message)
    { try { action(); } catch { Check(true, message); return; } throw new Exception(message); }
    private static void TokenTests()
    {
        foreach (string culture in new[] { "en-US", "de-DE", "fr-FR" })
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var expected = new NoteImageReference(Guid.NewGuid().ToString("N") + ".png", .75, Guid.NewGuid());
            Check(NoteImageReference.TryParse(expected.Token, out var restored) && restored == expected, "Invariant marker round trip " + culture);
        }
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        Check(NoteImageReference.TryParse("⟦IMG:old.png|r=0,50⟧", out var legacy) && legacy.WidthRatio == .5, "Legacy decimal-comma marker");
        Check(!NoteImageReference.TryParse("⟦IMG:../escape.png|r=0.3⟧", out _), "Reject asset path traversal");
        Check(!NoteImageReference.TryParse("[[PNIMG:2:future]]", out _), "Unknown marker version stays unresolved");
    }
    private static NoteImageControl Image(NoteImageReference reference)
    {
        var control = new NoteImageControl(); control.LoadReference(Path.Combine(Root, reference.FileName), reference, 300); return control;
    }
    private static void CodecTests()
    {
        var codec = new NoteDocumentCodec();
        var reference = new NoteImageReference("missing.png", .5, Guid.NewGuid());
        var image = Image(reference);
        var document = new FlowDocument { FontFamily = new FontFamily("Segoe UI"), FontSize = 17 };
        var paragraph = new Paragraph(); document.Blocks.Add(paragraph);
        var bold = new Bold(new Run("before ")); paragraph.Inlines.Add(bold);
        paragraph.Inlines.Add(new InlineUIContainer(image));
        paragraph.Inlines.Add(new Hyperlink(new Run("after")) { NavigateUri = new Uri("https://example.com") });
        var table = new Table(); var group = new TableRowGroup(); var row = new TableRow();
        row.Cells.Add(new TableCell(new Paragraph(new Run("table content")))); group.Rows.Add(row); table.RowGroups.Add(group); document.Blocks.Add(table);
        var originalParent = image.Parent;
        var snapshot = codec.Capture(document);
        Check(image.Parent == originalParent && ReferenceEquals(NoteDocumentCodec.Images(document).Single(), image), "Saving does not replace live image controls");
        Check(snapshot.Text.Contains("[Image]") && snapshot.Text.Contains("table content"), "Search text includes image placeholders and tables");
        var restored = codec.Load(snapshot.Rtf, Image);
        Check(NoteDocumentCodec.Images(restored).Single().Reference == reference, "Missing-image identity survives save/reopen");
        Check(codec.Capture(restored).Images.SequenceEqual(snapshot.Images), "Repeated saves retain missing assets");
        Check(NoteDocumentCodec.Images(codec.Load(codec.DuplicateRtf(snapshot.Rtf), Image)).Single().Reference.OccurrenceId != reference.OccurrenceId,
            "Duplicated document mints new occurrence identities");
        var paragraphs = NoteDocumentCodec.Paragraphs(restored.Blocks).ToArray();
        Check(paragraphs.Length == 2 && new TextRange(paragraphs[1].ContentStart, paragraphs[1].ContentEnd).Text.Contains("table content"), "Rich-paste table survives round trip");
        Check(new TextRange(paragraphs[0].ContentStart, paragraphs[0].ContentStart.GetPositionAtOffset(4)!).GetPropertyValue(TextElement.FontWeightProperty).Equals(FontWeights.Bold), "Bold formatting survives detached serialization");
        string token = reference.Token;
        var split = new FlowDocument(new Paragraph()); var p = (Paragraph)split.Blocks.FirstBlock!;
        p.Inlines.Add(new Run(token[..20])); p.Inlines.Add(new Bold(new Run(token[20..])));
        codec.RestoreImages(split, Image);
        Check(NoteDocumentCodec.Images(split).Count() == 1, "Marker split across formatted runs restores");
        var unsupported = new FlowDocument(new BlockUIContainer(new System.Windows.Controls.Button()));
        Throws(() => codec.Capture(unsupported), "Unsupported content fails visibly rather than disappearing");
        var fragment = codec.CaptureSelection(document, paragraph.ContentStart, paragraph.ContentEnd);
        Check(codec.Load(fragment.Rtf, Image) is FlowDocument selected && NoteDocumentCodec.Images(selected).Count() == 1, "Selected rich-text fragment retains images");
        var after = paragraph.Inlines.LastInline!;
        var textFragment = codec.CaptureSelection(document, after.ContentStart, after.ContentEnd);
        Check(textFragment.Images.Count == 0 && textFragment.Text == "after", "Selection offset mapping accounts for preceding images");
        var literal = new FlowDocument(new Paragraph(new Run(reference.Token + " [literal] ⟦IMG:typed.png⟧")));
        var literalSnapshot = codec.Capture(literal);
        var literalRestored = codec.Load(literalSnapshot.Rtf, Image);
        Check(!NoteDocumentCodec.Images(literalRestored).Any() && new TextRange(literalRestored.ContentStart, literalRestored.ContentEnd).Text.Contains(reference.Token), "Literal marker-shaped text does not become an image");
        var literalParagraph = (Paragraph)literal.Blocks.FirstBlock!;
        var literalFragment = codec.CaptureSelection(literal, literalParagraph.ContentStart.GetPositionAtOffset(2)!, literalParagraph.ContentEnd);
        Check(literalFragment.Text.StartsWith(reference.Token[1..]), "Selection mapping preserves literal bracket escapes");
        string externalRtf = codec.ExternalRtf(fragment.Rtf);
        var external = new FlowDocument(); using (var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(externalRtf)))
            new TextRange(external.ContentStart, external.ContentEnd).Load(input, System.Windows.DataFormats.Rtf);
        Check(new TextRange(external.ContentStart, external.ContentEnd).Text.Contains("[Image]"), "External clipboard RTF uses readable placeholders");
        var future = new FlowDocument(new Paragraph(new Run("[[PNIMG:2:future]]")));
        codec.RestoreImages(future, Image);
        Check(codec.Capture(future).Rtf.Contains("PNIMG:2:future"), "Unknown image versions remain opaque and recoverable");
    }
    private static BitmapSource Bitmap(int width, int height)
    {
        var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, new byte[width * height * 4], width * 4);
        source.Freeze(); return source;
    }
    private static void AssetTests()
    {
        var store = new ImageAssetStore(Path.Combine(Root, "assets")); var noteId = Guid.NewGuid();
        var reference = store.Import(noteId, Bitmap(1200, 900)); var path = store.Resolve(noteId, reference.FileName);
        Check(File.Exists(path), "Image file committed before document reference");
        var thumbnail = ImageDecodeService.Decode(path, 300);
        Check(thumbnail.PixelWidth == 300 && thumbnail.PixelHeight == 225, "Display-size decode uses 300 x 225 pixels");
        int smallWidth = ImageDecodeService.NotePreviewWidth(100);
        var smallPreview = ImageDecodeService.Decode(path, smallWidth);
        Check(smallPreview.PixelWidth == 80 && smallPreview.PixelHeight == 60, "Small previews use a lower-resolution bitmap");
        Check(ImageDecodeService.NotePreviewWidth(900) == 512, "Large note previews cap their decode width");
        ViewerPreviewTest(path);
        var portrait = store.Import(noteId, Bitmap(180, 2400));
        var portraitThumbnail = ImageDecodeService.Decode(store.Resolve(noteId, portrait.FileName), 300);
        Check(portraitThumbnail.PixelHeight == ImageDecodeService.MaxThumbnailEdge && portraitThumbnail.PixelWidth < 180,
            "Tall thumbnails cap their longest edge without stretching");
        var clone = Guid.NewGuid(); store.CopyReferenced(noteId, clone, new[] { reference.FileName });
        Check(File.Exists(store.Resolve(clone, reference.FileName)), "Duplicate copies referenced assets");
        string jpeg = Path.Combine(Root, "export.jpg"); ImageExportService.Export(path, jpeg);
        using (var input = File.OpenRead(jpeg)) Check(BitmapDecoder.Create(input, BitmapCreateOptions.None, BitmapCacheOption.OnLoad) is JpegBitmapDecoder, "JPEG export contains JPEG bytes");
        var importedOriginal = store.Import(noteId, jpeg);
        Check(File.ReadAllBytes(store.Resolve(noteId, importedOriginal.FileName)).SequenceEqual(File.ReadAllBytes(jpeg)), "File imports preserve original encoded bytes");
        string originalCopy = Path.Combine(Root, "original-copy.jpg"); ImageExportService.SaveOriginal(jpeg, originalCopy);
        Check(File.ReadAllBytes(originalCopy).SequenceEqual(File.ReadAllBytes(jpeg)), "Original export preserves encoded bytes");
        Throws(() => ImageExportService.Export(path, Path.Combine(Root, "wrong.webp")), "Reject mismatched unsupported export extension");
        string bad = Path.Combine(Root, "fake.png"); File.WriteAllText(bad, "not an image");
        Throws(() => store.Import(noteId, bad), "Reject invalid image content");
        Throws(() => store.Resolve(noteId, "../escape.png"), "Asset store rejects traversal");
        string atomic = Path.Combine(Root, "atomic.txt"); AtomicFile.WriteText(atomic, "before");
        Throws(() => AtomicFile.Write(atomic, output => { output.WriteByte(42); throw new IOException("injected failure"); }), "Inject interrupted write");
        Check(File.ReadAllText(atomic) == "before", "Interrupted write preserves committed bytes");
        AtomicFile.WriteText(atomic, "after"); Check(File.ReadAllText(atomic + ".bak") == "before", "Atomic replacement retains backup");
    }
    private static string Rtf(string text) => new NoteDocumentCodec().Capture(new FlowDocument(new Paragraph(new Run(text)))).Rtf;
    private static void StorageTests()
    {
        string path = Path.Combine(Root, "storage"); using var storage = new NoteStorageService(path);
        var note = new Note { Title = "Sample", Text = "content", RtfText = Rtf("content"), IsClosed = true };
        var notes = new List<Note> { note };
        Check(storage.SaveNotesImmediate(notes), "Durable note save");
        Check(!File.ReadAllText(Path.Combine(path, "index.json")).Contains("RtfText"), "Metadata index does not duplicate RTF");
        var loaded = storage.LoadNotes().Single();
        Check(!loaded.IsContentLoaded && loaded.RtfText == "", "Closed-note RTF loads lazily");
        storage.EnsureContentLoaded(loaded); Check(loaded.RtfText.Contains("content"), "Opening closed note loads content");
        storage.SaveNotesDebounced(new() { note }, 100);
        var newer = new Note { Id = note.Id, Title = note.Title, Text = "newer", RtfText = Rtf("newer") };
        Check(storage.SaveNotesImmediate(new() { newer }), "Immediate save supersedes queued snapshot");
        Thread.Sleep(150);
        Check(storage.LoadNotes().Single().RtfText.Contains("newer"), "Stale queued snapshot cannot overwrite new content");
        string assets = Path.Combine(path, "Images", note.Id.ToString("N")); Directory.CreateDirectory(assets);
        File.WriteAllText(Path.Combine(assets, "undo.png"), "retained");
        Check(storage.SaveNotesImmediate(new()), "Note deletion metadata commits");
        Check(File.Exists(Path.Combine(assets, "undo.png")), "Save does not destroy retained image assets");
        string recoverPath = Path.Combine(Root, "identity-recovery");
        using var recovery = new NoteStorageService(recoverPath);
        Check(recovery.SaveNotesImmediate(new() { newer }), "Save recoverable document identity");
        File.Move(Path.Combine(recoverPath, "index.json"), Path.Combine(recoverPath, "index.test-hidden"));
        Check(recovery.LoadNotes().Single().Id == newer.Id, "RTF retains full note identity when metadata is interrupted");
        string recoveredFile = Directory.GetFiles(Path.Combine(recoverPath, "Notes"), "*.rtf").Single();
        Check(recovery.SaveNotesImmediate(new() { new Note { Id = newer.Id, Title = newer.Title, Text = "latest", RtfText = Rtf("latest") } }), "Create known-good backup revision");
        File.WriteAllText(recoveredFile, "{\\rtf1 incomplete");
        var recovered = recovery.LoadNotes().Single();
        Check(recovered.RtfText.Contains("newer"), "Corrupt live RTF recovers previous committed content");
        Check(Directory.GetFiles(Path.Combine(recoverPath, "Recovery")).Length == 1, "Corrupt bytes are retained separately for recovery");
        Check(recovery.SaveNotesImmediate(new() { recovered }) && File.Exists(recoveredFile), "Recovered content can be durably saved again");
        using var plain = new NoteStorageService(Path.Combine(Root, "plain-fallback"));
        var plainNote = new Note { Text = "Unicode ✓ {braces} [literal]", Title = "Plain" };
        Check(plain.SaveNotesImmediate(new() { plainNote }), "Plain-text legacy note creates valid RTF");
        var plainLoaded = plain.LoadNotes().Single();
        var plainDoc = new NoteDocumentCodec().Load(plainLoaded.RtfText, Image);
        Check(new TextRange(plainDoc.ContentStart, plainDoc.ContentEnd).Text.Contains(plainNote.Text), "Plain-text fallback preserves Unicode, braces and brackets");
        string dropped = Path.Combine(Root, "plain-fallback", "Notes", "deadbeef.rtf");
        File.WriteAllText(dropped, Rtf("dropped while running"));
        Check(plain.SaveNotesImmediate(new() { plainLoaded }) && File.Exists(dropped), "Saving leaves files added outside the app in place");
        Check(plain.LoadNotes().Any(n => n.Title == "deadbeef"), "An eight-character hexadecimal filename loads without a title prefix");
    }
    private static void HistoryTests()
    {
        using var history = new NoteEditorHistory();
        EditorCheckpoint State(string value) => new(new(value, value, Array.Empty<string>()), 0, 0);
        history.Record(State("text")); history.Record(State("text+image")); history.Record(State("resized"));
        Check(history.Undo()?.Document.Rtf == "text+image", "Resize undo is ordered with document edits");
        Check(history.Undo()?.Document.Rtf == "text", "Image insertion undo");
        Check(history.Redo()?.Document.Rtf == "text+image", "Image insertion redo");
        history.Record(State("new branch")); Check(!history.CanRedo, "New edit invalidates redo branch");
    }
    private static void MigrationTests()
    {
        string source = Path.Combine(Root, "migration-source"); string target = Path.Combine(Root, "migration-target");
        Directory.CreateDirectory(Path.Combine(source, "Images", "note"));
        File.WriteAllText(Path.Combine(source, "Images", "note", "asset.png"), "test asset bytes");
        StorageMigrationService.CopyVerified(source, target);
        Check(File.Exists(Path.Combine(source, "Images", "note", "asset.png")), "Migration retains source for rollback");
        Check(File.ReadAllText(Path.Combine(target, "Images", "note", "asset.png")) == "test asset bytes", "Migration copies and verifies assets");
        Throws(() => StorageMigrationService.CopyVerified(source, Path.Combine(source, "nested")), "Reject nested migration destinations");
        string occupied = Path.Combine(Root, "occupied"); Directory.CreateDirectory(occupied); File.WriteAllText(Path.Combine(occupied, "user.txt"), "keep");
        Throws(() => StorageMigrationService.CopyVerified(source, occupied), "Reject overwriting occupied destination");
        StorageMigrationService.CopyVerified(source, target);
        Check(File.Exists(Path.Combine(target, "Images", "note", "asset.png")), "Completed migration can be safely retried before switching settings");
        using var destination = new NoteStorageService(target);
        Check(destination.SaveNotesImmediate(new() { new Note { Title = "Destination edit", RtfText = Rtf("keep") } }), "Migrated store accepts edits");
        Throws(() => StorageMigrationService.CopyVerified(source, target), "Migration cannot overwrite a destination that has been used");
    }
    private static void CollectionTests()
    {
        string root = Path.Combine(Root, "collection"); var id = Guid.NewGuid();
        var store = new ImageAssetStore(root); var used = store.Import(id, Bitmap(10, 10)); var unused = store.Import(id, Bitmap(10, 10));
        File.SetLastWriteTimeUtc(store.Resolve(id, used.FileName), DateTime.UtcNow.AddDays(-10));
        File.SetLastWriteTimeUtc(store.Resolve(id, unused.FileName), DateTime.UtcNow.AddDays(-10));
        Directory.CreateDirectory(Path.Combine(root, "Notes"));
        File.WriteAllText(Path.Combine(root, "index.json"), "[]");
        var doc = new FlowDocument(new Paragraph(new InlineUIContainer(Image(used))));
        File.WriteAllText(Path.Combine(root, "Notes", "Sample_" + NoteStorageService.GetShortId(id) + ".rtf"), new NoteDocumentCodec().Capture(doc).Rtf);
        Check(ImageAssetMaintenance.CollectAtColdStart(root, DateTime.UtcNow) == 1, "Cold-start collection quarantines only unreferenced old assets");
        Check(File.Exists(store.Resolve(id, used.FileName)), "Collection retains document assets");
        Check(File.Exists(store.Resolve(id, unused.FileName)), "Resolving a quarantined asset restores it for recovery");
        File.Move(Path.Combine(root, "index.json"), Path.Combine(root, "index.test-hidden"));
        Throws(() => ImageAssetMaintenance.CollectAtColdStart(root, DateTime.UtcNow), "Missing metadata blocks destructive asset collection");
        File.Move(Path.Combine(root, "index.test-hidden"), Path.Combine(root, "index.json"));
        string corrupt = Path.Combine(root, "Notes", "bad.rtf"); File.WriteAllText(corrupt, "not rtf");
        Throws(() => ImageAssetMaintenance.CollectAtColdStart(root, DateTime.UtcNow), "Incomplete recovery scan aborts collection");
    }
    private static void SessionTests()
    {
        var native = new System.Windows.Controls.RichTextBox();
        var nativeImage = Image(new("native.png", .3, Guid.NewGuid()));
        native.Document = new FlowDocument(new Paragraph(new InlineUIContainer(nativeImage)));
        native.IsUndoEnabled = false; native.IsUndoEnabled = true;
        nativeImage.SetWidthRatio(.75, 300);
        Check(!native.CanUndo, "Native undo does not record image-control property resizing; unified history is required");
        using var storage = new NoteStorageService(Path.Combine(Root, "session"));
        var window = new NoteWindow(new Note());
        var editor = (System.Windows.Controls.RichTextBox)window.FindName("ContentRichTextBox");
        window.NoteModel.Images = new() { "recovered.png" };
        typeof(NoteWindow).GetMethod("RestoreReferencesWithoutDocument", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, null);
        Check(NoteDocumentCodec.Images(editor.Document).Any(i => i.FileName == "recovered.png"), "Metadata image references remain visible when the entire RTF is missing");
        editor.Document.Blocks.Clear(); window.NoteModel.Images.Clear();
        typeof(NoteWindow).GetField("_isInitializing", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(window, false);
        typeof(NoteWindow).GetMethod("InitializeImageEditing", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, null);
        editor.Document.Blocks.Clear(); editor.Document.Blocks.Add(new Paragraph(new Run("typed")));
        Check(window.FlushDocument(), "Editor captures pending text");
        var reference = new NoteImageReference("missing.png", .3, Guid.NewGuid());
        var control = Image(reference);
        ((Paragraph)editor.Document.Blocks.FirstBlock!).Inlines.Add(new InlineUIContainer(control));
        Check(window.FlushDocument() && window.NoteModel.Images.Count == 1, "Editor captures image-only structural edits");
        System.Windows.Input.ApplicationCommands.Undo.Execute(null, editor);
        Check(window.NoteModel.Images.Count == 0 && window.NoteModel.Text.Contains("typed"), "Routed Undo restores text before image insertion");
        System.Windows.Input.ApplicationCommands.Redo.Execute(null, editor);
        Check(window.NoteModel.Images.Count == 1, "Routed Redo restores image reference");
        var current = NoteDocumentCodec.Images(editor.Document).Single(); current.SetWidthRatio(.75, 300);
        Check(NoteDocumentCodec.Images(editor.Document).Single().WidthRatio == .75, "Image resizing records a document edit");
        System.Windows.Input.ApplicationCommands.Undo.Execute(null, editor);
        Check(NoteDocumentCodec.Images(editor.Document).Single().WidthRatio == .3, "Image resizing participates in unified Undo");
        typeof(NoteWindow).GetField("_activeImageControl", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(window, NoteDocumentCodec.Images(editor.Document).Single());
        typeof(NoteWindow).GetMethod("DeleteImageBtn_Click", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(window, new object[] { window, new RoutedEventArgs() });
        Check(!NoteDocumentCodec.Images(editor.Document).Any(), "Image action removes the occurrence");
        System.Windows.Input.ApplicationCommands.Undo.Execute(null, editor);
        Check(NoteDocumentCodec.Images(editor.Document).Count() == 1, "Undo restores removed image occurrence");
        var sourceRoot = Path.Combine(Root, "clipboard-source"); var sourceId = Guid.NewGuid(); var assetStore = new ImageAssetStore(sourceRoot);
        var asset = assetStore.Import(sourceId, Bitmap(20, 20));
        var sourceDoc = new FlowDocument(new Paragraph(new InlineUIContainer(Image(asset))));
        var payload = new NoteClipboardPayload(sourceRoot, sourceId, new NoteDocumentCodec().Capture(sourceDoc).Rtf);
        var lastParagraph = NoteDocumentCodec.Paragraphs(editor.Document.Blocks).Last();
        lastParagraph.Inlines.Add(new Run(asset.Token));
        editor.Selection.Select(lastParagraph.ContentEnd, lastParagraph.ContentEnd); window.FlushDocument();
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
        try
        {
            var paste = (Task)typeof(NoteWindow).GetMethod("PasteDocumentAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(window, new object[] { System.Text.Json.JsonSerializer.Serialize(payload) })!;
            if (!paste.IsCompleted)
            {
                var frame = new System.Windows.Threading.DispatcherFrame();
                paste.ContinueWith(_ => frame.Continue = false, TaskScheduler.FromCurrentSynchronizationContext());
                System.Windows.Threading.Dispatcher.PushFrame(frame);
            }
            paste.GetAwaiter().GetResult();
        }
        finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
        Check(NoteDocumentCodec.Images(editor.Document).Count() == 2, "Cross-note paste imports images without interpreting existing literal markers");
        var pasted = NoteDocumentCodec.Images(editor.Document).Last();
        Check(pasted.Reference.OccurrenceId != asset.OccurrenceId && File.Exists(pasted.FullPath), "Cross-note paste owns its asset and occurrence identity");
        window.NoteModel.IsLocked = true;
        Check(!System.Windows.Input.ApplicationCommands.Undo.CanExecute(null, editor), "Locked editor rejects Undo mutation");
        window.NoteModel.IsLocked = false;
        var history = Suspend(window);
        Check(history.CanUndo, "Suspending a note releases its window without discarding Undo");
        var resumed = new NoteWindow(window.NoteModel, history);
        var resumedEditor = (System.Windows.Controls.RichTextBox)resumed.FindName("ContentRichTextBox");
        resumedEditor.Document = new NoteDocumentCodec().Load(window.NoteModel.RtfText, Image);
        typeof(NoteWindow).GetField("_isInitializing", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(resumed, false);
        typeof(NoteWindow).GetMethod("InitializeImageEditing", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(resumed, null);
        System.Windows.Input.ApplicationCommands.Undo.Execute(null, resumedEditor);
        Check(NoteDocumentCodec.Images(resumedEditor.Document).Count() == 1, "Reopening a suspended note can undo the previous edit");
        resumed.Close();
    }

    private static NoteEditorHistory Suspend(NoteWindow window) =>
        (NoteEditorHistory)typeof(NoteWindow).GetMethod("SuspendForHide", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(window, null)!;
    private static void ViewerPreviewTest(string path)
    {
        static void WaitFor(Func<bool> ready)
        {
            var frame = new System.Windows.Threading.DispatcherFrame();
            var deadline = Stopwatch.StartNew();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
            timer.Tick += (_, _) =>
            {
                if (!ready() && deadline.Elapsed < TimeSpan.FromSeconds(5)) return;
                timer.Stop();
                frame.Continue = false;
            };
            timer.Start();
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }

        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
        try
        {
            var viewer = new ImageViewerWindow(path);
            try
            {
                var image = (System.Windows.Controls.Image)viewer.FindName("ViewerImage");
                WaitFor(() => image.Source != null);
                var preview = image.Source as BitmapSource;
                double originalWidth = image.Width;
                Check(preview?.PixelWidth == 1024 && originalWidth > preview.Width, "Viewer starts with a bounded preview and keeps original layout size");
                typeof(ImageViewerWindow).GetMethod("ApplyZoom", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(viewer, new object[] { 1.0 });
                WaitFor(() => image.Source is BitmapSource { PixelWidth: 1200 });
                Check(image.Source is BitmapSource { PixelWidth: 1200 } && image.Width == originalWidth,
                    "Actual-size zoom loads original detail without changing image layout");
                typeof(ImageViewerWindow).GetMethod("FitToScreen", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(viewer, null);
                Check(ReferenceEquals(image.Source, preview), "Fit releases the viewer's original-resolution bitmap");
            }
            finally { viewer.Close(); }
        }
        finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
    }

    private static void ThumbnailLifetimeTest()
    {
        var control = Image(new("resize.png", .3, Guid.NewGuid()));
        var display = (System.Windows.Controls.Image)control.FindName("DisplayImage");
        var bitmap = Bitmap(80, 60);
        display.Source = bitmap;
        control.UpdateResponsiveWidth(500, refreshThumbnail: false);
        control.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Check(ReferenceEquals(display.Source, bitmap), "Inline image keeps its thumbnail when detached during resize");

        control.UpdateResponsiveWidth(500);
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.ApplicationIdle)
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        Check(display.Source == null, "A thumbnail still detached after resize is released");
    }

    private static void BenchmarkMemoryBreakdown()
    {
        string path = BenchmarkImage();
        foreach (string stage in new[] { "wpf", "transparent", "editor", "note", "image", "decode-full", "viewer-fit", "viewer-zoom-preview", "viewer-full" })
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true
            };
            start.ArgumentList.Add("--memory-stage");
            start.ArgumentList.Add(stage);
            start.ArgumentList.Add(path);
            using var process = Process.Start(start)!;
            Console.WriteLine(process.StandardOutput.ReadToEnd().Trim());
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException($"Memory stage {stage} failed.");
        }
    }

    private static void MeasureMemoryStage(string stage, string imagePath)
    {
        const double MiB = 1024 * 1024;
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        long baseline = process.PrivateMemorySize64;
        if (stage == "decode-full")
        {
            var bitmap = ImageDecodeService.Decode(imagePath);
            process.Refresh();
            Console.WriteLine($"decode-full: baseline {baseline / MiB:F1}, private {process.PrivateMemorySize64 / MiB:F1}, " +
                $"working {process.WorkingSet64 / MiB:F1}, managed {GC.GetTotalMemory(false) / MiB:F1} MiB, decoded width {bitmap.PixelWidth}");
            GC.KeepAlive(bitmap);
            return;
        }
        SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
        int pixelWidth = 0;
        Window window;
        if (stage is "wpf" or "transparent" or "editor")
        {
            window = new Window { Width = 900, Height = 620 };
            if (stage is "transparent" or "editor")
            {
                window.WindowStyle = WindowStyle.None;
                window.AllowsTransparency = true;
                window.Background = Brushes.Transparent;
                window.Content = new System.Windows.Controls.Border { Background = Brushes.LightYellow };
            }
            if (stage == "editor")
                window.Content = new System.Windows.Controls.RichTextBox(new FlowDocument(new Paragraph(new Run("A sample note."))));
        }
        else if (stage.StartsWith("viewer-", StringComparison.Ordinal))
        {
            window = new ImageViewerWindow(imagePath);
        }
        else
        {
            var note = new Note { Title = "Memory breakdown", Text = "A sample note.", Width = 900, Height = 620, X = -10000, Y = -10000 };
            if (stage == "image")
            {
                using var storage = new NoteStorageService(Path.Combine(Root, "memory-stage"));
                var assets = new ImageAssetStore(NoteStorageService.GetEffectiveStoragePath());
                string folder = assets.DirectoryFor(note.Id);
                Directory.CreateDirectory(folder);
                string fileName = Path.GetFileName(imagePath);
                File.Copy(imagePath, Path.Combine(folder, fileName));
                var image = Image(new(fileName, 1, Guid.NewGuid()));
                note.RtfText = new NoteDocumentCodec().Capture(new FlowDocument(new Paragraph(new InlineUIContainer(image)))).Rtf;
            }
            window = new NoteWindow(note);
        }

        window.Left = -10000;
        window.Top = -10000;
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.Show();
        PumpDispatcher(TimeSpan.FromSeconds(1.5));
        if (stage.StartsWith("viewer-", StringComparison.Ordinal))
        {
            var image = (System.Windows.Controls.Image)window.FindName("ViewerImage");
            if (stage is "viewer-full" or "viewer-zoom-preview")
            {
                string method = stage == "viewer-full" ? "ApplyZoom" : "UpdateZoom";
                typeof(ImageViewerWindow).GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(window, new object[] { 1.0 });
                PumpDispatcher(TimeSpan.FromSeconds(1.5));
            }
            pixelWidth = ((BitmapSource?)image.Source)?.PixelWidth ?? 0;
            if (pixelWidth != (stage == "viewer-full" ? 4000 : 1024))
                throw new InvalidOperationException($"Unexpected viewer decode size: {pixelWidth}.");
        }
        else if (stage == "image")
        {
            var editor = (System.Windows.Controls.RichTextBox)window.FindName("ContentRichTextBox");
            pixelWidth = NoteDocumentCodec.Images(editor.Document).Single().ImageBitmap?.PixelWidth ?? 0;
            if (pixelWidth != ImageDecodeService.MaxNotePreviewWidth)
                throw new InvalidOperationException($"Unexpected inline decode size: {pixelWidth}.");
        }
        process.Refresh();
        Console.WriteLine($"{stage}: baseline {baseline / MiB:F1}, private {process.PrivateMemorySize64 / MiB:F1}, " +
            $"working {process.WorkingSet64 / MiB:F1}, managed {GC.GetTotalMemory(false) / MiB:F1} MiB, decoded width {pixelWidth}");
        window.Close();
    }

    private static void PumpDispatcher(TimeSpan interval)
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = interval };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    private static void BenchmarkNotes()
    {
        const double MiB = 1024 * 1024;
        var codec = new NoteDocumentCodec();
        var document = new FlowDocument();
        for (int i = 0; i < 250; i++) document.Blocks.Add(new Paragraph(new Run(new string('a', 800))));
        string rtf = codec.Capture(document).Rtf;
        long before = GC.GetAllocatedBytesForCurrentThread();
        var loaded = codec.Load(rtf, Image);
        Console.WriteLine($"Document load allocated {(GC.GetAllocatedBytesForCurrentThread() - before) / MiB:F2} MiB on the UI thread");
        GC.KeepAlive(loaded);

        using var process = Process.GetCurrentProcess();
        void Sample(string phase)
        {
            PumpDispatcher(TimeSpan.FromMilliseconds(900));
            process.Refresh();
            Console.WriteLine($"{phase}: private {process.PrivateMemorySize64 / MiB:F1} MiB, working set {process.WorkingSet64 / MiB:F1} MiB, managed {GC.GetTotalMemory(false) / MiB:F1} MiB");
        }
        using var storage = new NoteStorageService(Path.Combine(Root, "note-benchmark"));
        var store = new ImageAssetStore(NoteStorageService.GetEffectiveStoragePath());
        string original = BenchmarkImage();
        var models = new List<Note>();
        for (int i = 0; i < 4; i++)
        {
            var note = new Note { Title = "Memory benchmark", Width = 900, Height = 620, X = -10000, Y = -10000 };
            var image = store.Import(note.Id, original) with { WidthRatio = 1.0 };
            note.RtfText = codec.Capture(new FlowDocument(new Paragraph(new InlineUIContainer(Image(image))))).Rtf;
            models.Add(note);
        }
        Sample("Before windows");
        var windows = models.Select(note => new NoteWindow(note) { ShowActivated = false }).ToList();
        foreach (var window in windows) window.Show();
        Sample("Four image notes");
        foreach (var window in windows) window.Hide();
        Sample("Hidden notes");
        var histories = windows.Select(Suspend).ToArray();
        windows.Clear();
        ImageDecodeService.Clear();
        Sample("Suspended notes");
        foreach (var history in histories) history.Dispose();
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static string BenchmarkImage()
    {
        var store = new ImageAssetStore(Root); var id = Guid.NewGuid();
        var reference = store.Import(id, Bitmap(4000, 3000)); return store.Resolve(id, reference.FileName);
    }
    private static int MemoryBenchmark(bool full, string path)
    {
        _ = ImageDecodeService.Decode(path, 64);
        GC.Collect(2, GCCollectionMode.Forced, true, true); GC.WaitForPendingFinalizers(); GC.Collect(2, GCCollectionMode.Forced, true, true);
        using var process = Process.GetCurrentProcess(); process.Refresh(); long baseline = process.PrivateMemorySize64;
        var images = new List<BitmapSource>(); var watch = Stopwatch.StartNew();
        for (int i = 0; i < 5; i++) images.Add(ImageDecodeService.Decode(path, full ? 0 : 300));
        watch.Stop(); process.Refresh();
        long pixels = images.Sum(i => (long)i.PixelWidth * i.PixelHeight * 4);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
        {
            Mode = full ? "full" : "thumbnail", Count = images.Count, PixelBufferMiB = pixels / 1048576.0,
            PrivateDeltaMiB = (process.PrivateMemorySize64 - baseline) / 1048576.0,
            WorkingSetMiB = process.WorkingSet64 / 1048576.0, ElapsedMs = watch.ElapsedMilliseconds
        }));
        GC.KeepAlive(images); return 0;
    }
    private static void MemoryComparisons()
    {
        string path = BenchmarkImage();
        foreach (string mode in new[] { "--memory-full", "--memory-thumbnail" })
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!)
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(mode); start.ArgumentList.Add(path);
            using var child = Process.Start(start)!;
            string output = child.StandardOutput.ReadToEnd(); string error = child.StandardError.ReadToEnd(); child.WaitForExit();
            Check(child.ExitCode == 0, "Isolated memory benchmark " + mode);
            Console.WriteLine(output.Trim());
            if (!string.IsNullOrEmpty(error)) Console.WriteLine(error);
        }
    }
    private static void RenderPreview()
    {
        var window = new NoteWindow(new Note { Title = "Image support preview" });
        var editor = (System.Windows.Controls.RichTextBox)window.FindName("ContentRichTextBox");
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(Brushes.SteelBlue, null, new Rect(0, 0, 600, 360));
            drawing.DrawRectangle(Brushes.LightSkyBlue, null, new Rect(40, 40, 240, 280));
            drawing.DrawEllipse(Brushes.White, null, new Point(420, 180), 90, 90);
        }
        var bitmap = new RenderTargetBitmap(600, 360, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze();
        var image = Image(new("preview.png", .75, Guid.NewGuid()));
        ((System.Windows.Controls.Image)image.FindName("DisplayImage")).Source = bitmap;
        editor.Document.Blocks.Clear();
        editor.Document.Blocks.Add(new Paragraph(new Run("Keep screenshots with your notes.")));
        editor.Document.Blocks.Add(new Paragraph(new InlineUIContainer(image)));
        editor.Document.Blocks.Add(new Paragraph(new Run("Images resize with the note.")));
        var root = (FrameworkElement)window.Content; window.Content = null;
        root.Measure(new Size(340, 340)); root.Arrange(new Rect(0, 0, 340, 340)); root.UpdateLayout();
        var preview = new RenderTargetBitmap(340, 340, 96, 96, PixelFormats.Pbgra32); preview.Render(root);
        string path = Path.Combine(Root, "image-note-preview.png");
        using (var output = File.Create(path)) { var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(preview)); png.Save(output); }
        Check(preview.PixelWidth == 340 && File.Exists(path), "Render note layout with inline image");
        Console.WriteLine("PREVIEW " + path); window.Close();
        var viewer = new ImageViewerWindow(bitmap);
        typeof(ImageViewerWindow).GetMethod("ApplyZoom", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(viewer, new object[] { 3.0 });
        var viewerRoot = (FrameworkElement)viewer.Content; viewer.Content = null;
        viewerRoot.Measure(new Size(820, 620)); viewerRoot.Arrange(new Rect(0, 0, 820, 620)); viewerRoot.UpdateLayout();
        var viewerPreview = new RenderTargetBitmap(820, 620, 96, 96, PixelFormats.Pbgra32); viewerPreview.Render(viewerRoot);
        string viewerPath = Path.Combine(Root, "image-viewer-preview.png");
        using (var output = File.Create(viewerPath)) { var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(viewerPreview)); png.Save(output); }
        Console.WriteLine("VIEWER PREVIEW " + viewerPath); viewer.Close();
    }
}
