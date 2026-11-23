using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopNotes.Models;
using DesktopNotes.Services;

namespace DesktopNotes.Views;

public partial class NoteWindow
{
    private bool _isSerializing, _documentDirty, _closed;
    private bool _importing;
    private long _editVersion;
    private NoteImageControl? _activeImageControl;
    private readonly NoteDocumentCodec _documentCodec = new();
    private readonly NoteEditorHistory _history;
    internal bool IsSuspending { get; private set; }
    private const string ClipboardFormat = "PermaNotes.Document.v1";
    private DispatcherTimer? _captureTimer;
    private ImageAssetStore Assets => new(NoteStorageService.GetEffectiveStoragePath());
    private double EditorWidth => ContentRichTextBox.ActualWidth > 50 ? ContentRichTextBox.ActualWidth : Math.Max(100, Width - 32);
    private static bool IsImageFile(string path) => Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp";
    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T match) return match;
            child = child switch
            {
                FrameworkContentElement content => content.Parent,
                System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D => System.Windows.Media.VisualTreeHelper.GetParent(child),
                _ => null
            };
        }
        return null;
    }

    private NoteImageControl CreateImage(NoteImageReference reference)
    {
        var control = new NoteImageControl { CanEdit = !NoteModel.IsLocked };
        control.LoadReference(Assets.Resolve(NoteModel.Id, reference.FileName), reference, EditorWidth);
        control.DoubleClicked += NoteImageControl_DoubleClicked;
        control.ResizeStarted += (_, _) => FlushDocument();
        control.Resized += (_, _) => { if (!NoteModel.IsLocked) { _documentDirty = true; FlushDocument(); } };
        return control;
    }
    public void DeserializeImagesInDocument() => _documentCodec.RestoreImages(ContentRichTextBox.Document, CreateImage);
    private void RestoreReferencesWithoutDocument()
    {
        if (!string.IsNullOrEmpty(NoteModel.RtfText) || NoteModel.Images.Count == 0) return;
        ContentRichTextBox.Document.Blocks.Add(new Paragraph(new Run("Recovered images (original placement unavailable):")));
        foreach (string file in NoteModel.Images.Where(NoteImageReference.IsSafeFileName).Distinct())
            ContentRichTextBox.Document.Blocks.Add(new Paragraph(new InlineUIContainer(CreateImage(new(file, .3, Guid.NewGuid())))));
    }
    private void InitializeImageEditing()
    {
        ContentRichTextBox.IsUndoEnabled = false;
        var formatting = new HashSet<ICommand> { EditingCommands.ToggleBold, EditingCommands.ToggleItalic,
            EditingCommands.ToggleUnderline, EditingCommands.ToggleBullets, EditingCommands.ToggleNumbering,
            EditingCommands.IncreaseFontSize, EditingCommands.DecreaseFontSize };
        ContentRichTextBox.AddHandler(CommandManager.PreviewExecutedEvent, new ExecutedRoutedEventHandler((_, e) =>
        { if (formatting.Contains(e.Command) && !FlushDocument()) e.Handled = true; }), true);
        ContentRichTextBox.AddHandler(CommandManager.ExecutedEvent, new ExecutedRoutedEventHandler((_, e) =>
        { if (formatting.Contains(e.Command)) { ScheduleDocumentCapture(); FlushDocument(); } }), true);
        ContentRichTextBox.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy,
            (_, e) => { CopySelection(false); e.Handled = true; },
            (_, e) => { e.CanExecute = !ContentRichTextBox.Selection.IsEmpty; e.Handled = true; }));
        ContentRichTextBox.CommandBindings.Add(new CommandBinding(ApplicationCommands.Cut,
            (_, e) => { CopySelection(true); e.Handled = true; },
            (_, e) => { e.CanExecute = !NoteModel.IsLocked && !ContentRichTextBox.Selection.IsEmpty; e.Handled = true; }));
        _captureTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(400), DispatcherPriority.Background, (_, _) => FlushDocument(), Dispatcher);
        _captureTimer.Stop();
        ContentRichTextBox.CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo,
            (_, e) => { RestoreHistory(false); e.Handled = true; },
            (_, e) => { e.CanExecute = !NoteModel.IsLocked && (_documentDirty || _history.CanUndo); e.Handled = true; }));
        ContentRichTextBox.CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo,
            (_, e) => { RestoreHistory(true); e.Handled = true; },
            (_, e) => { e.CanExecute = !NoteModel.IsLocked && _history.CanRedo; e.Handled = true; }));
        var initial = _documentCodec.Capture(ContentRichTextBox.Document);
        NoteModel.RtfText = initial.Rtf; NoteModel.Text = initial.Text; NoteModel.Images = initial.Images.ToList();
        if (_history.Current is { } checkpoint) RestoreImageEditorSelection(checkpoint);
        else RecordHistory(initial);
        Closing += (_, e) => { if (!FlushDocument()) e.Cancel = true; };
        Closed += (_, _) =>
        {
            _closed = true;
            _captureTimer.Stop();
            foreach (var image in NoteDocumentCodec.Images(ContentRichTextBox.Document))
                image.ReleaseThumbnail();
            if (!IsSuspending) _history.Dispose();
        };
    }

    internal NoteEditorHistory? SuspendForHide()
    {
        if (_importing) { Hide(); return null; }
        if (!FlushDocument()) return null;
        var start = ContentRichTextBox.Document.ContentStart;
        var selection = ContentRichTextBox.Selection;
        _history.UpdateSelection(start.GetOffsetToPosition(selection.Start), start.GetOffsetToPosition(selection.End));
        IsSuspending = true;
        Close();
        return _history;
    }
    private void CopySelection(bool cut)
    {
        if (cut && NoteModel.IsLocked || !FlushDocument()) return;
        try
        {
            var selection = ContentRichTextBox.Selection;
            var snapshot = _documentCodec.CaptureSelection(ContentRichTextBox.Document, selection.Start, selection.End);
            var data = new System.Windows.DataObject();
            data.SetData(System.Windows.DataFormats.UnicodeText, snapshot.Text);
            data.SetData(System.Windows.DataFormats.Rtf, _documentCodec.ExternalRtf(snapshot.Rtf));
            if (snapshot.Images.Count > 0) data.SetData(ClipboardFormat, System.Text.Json.JsonSerializer.Serialize(
                new NoteClipboardPayload(NoteStorageService.GetEffectiveStoragePath(), NoteModel.Id, snapshot.Rtf)));
            System.Windows.Clipboard.SetDataObject(data, true);
            if (cut) { selection.Text = ""; _documentDirty = true; FlushDocument(); }
        }
        catch (Exception ex) { System.Windows.MessageBox.Show(this, ex.Message, "Copy note content"); }
    }

    private async Task PasteDocumentAsync(string json)
    {
        if (NoteModel.IsLocked || _closed || _importing || !FlushDocument()) return;
        _importing = true;
        var anchor = ContentRichTextBox.Selection.Start; var end = ContentRichTextBox.Selection.End;
        var original = ContentRichTextBox.Document; long version = _editVersion;
        try
        {
            var payload = System.Text.Json.JsonSerializer.Deserialize<NoteClipboardPayload>(json)
                ?? throw new InvalidDataException("Clipboard content is invalid.");
            var source = new ImageAssetStore(payload.StorageRoot); var destination = Assets;
            var imported = new Dictionary<string, NoteImageReference>(StringComparer.OrdinalIgnoreCase);
            var fragment = _documentCodec.Load(payload.Rtf, reference =>
            { var image = new NoteImageControl(); image.LoadReference(source.Resolve(payload.NoteId, reference.FileName), reference, EditorWidth); return image; });
            foreach (var image in NoteDocumentCodec.Images(fragment))
            {
                if (!imported.ContainsKey(image.FileName))
                {
                    if (File.Exists(image.FullPath)) imported[image.FileName] = await destination.ImportAsync(NoteModel.Id, image.FullPath);
                    else imported[image.FileName] = image.Reference;
                }
            }
            if (_closed || NoteModel.IsLocked || original != ContentRichTextBox.Document || destination.StorageRoot != Path.GetFullPath(NoteStorageService.GetEffectiveStoragePath())) return;
            foreach (var image in NoteDocumentCodec.Images(fragment).ToArray())
            {
                var reference = imported[image.FileName] with { WidthRatio = image.WidthRatio, OccurrenceId = Guid.NewGuid() };
                if (image.Parent is InlineUIContainer container) container.Child = CreateImage(reference);
            }
            var snapshot = _documentCodec.Capture(fragment);
            if (!FlushDocument()) return;
            _isSerializing = true;
            try
            {
                var range = new TextRange(anchor, version == _editVersion ? end : anchor);
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(snapshot.Rtf));
                range.Load(stream, System.Windows.DataFormats.Rtf);
                _documentCodec.RestoreImages(ContentRichTextBox.Document, CreateImage, range.Start, range.End);
                ContentRichTextBox.CaretPosition = range.End;
            }
            finally { _isSerializing = false; }
            _documentDirty = true; FlushDocument();
        }
        catch (Exception ex) { System.Windows.MessageBox.Show(this, ex.Message, "Paste note content"); }
        finally { _importing = false; }
    }

    private async Task RelinkImageAsync(NoteImageControl image)
    {
        if (NoteModel.IsLocked || _importing || !FlushDocument()) return;
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif" };
        if (dialog.ShowDialog(this) != true) return;
        var document = ContentRichTextBox.Document; var store = Assets;
        _importing = true;
        try
        {
            var reference = await store.ImportAsync(NoteModel.Id, dialog.FileName);
            if (_closed || NoteModel.IsLocked || document != ContentRichTextBox.Document || image.Parent is not InlineUIContainer ||
                store.StorageRoot != Path.GetFullPath(NoteStorageService.GetEffectiveStoragePath()) || !FlushDocument()) return;
            image.LoadReference(store.Resolve(NoteModel.Id, reference.FileName), reference with
                { WidthRatio = image.WidthRatio, OccurrenceId = image.Reference.OccurrenceId }, EditorWidth);
            _documentDirty = true; FlushDocument();
        }
        catch (Exception ex) { System.Windows.MessageBox.Show(this, ex.Message, "Replace image"); }
        finally { _importing = false; }
    }
    private void ScheduleDocumentCapture()
    {
        if (_isInitializing || _isSerializing || NoteModel.IsLocked || _closed) return;
        _documentDirty = true; _captureTimer?.Stop(); _captureTimer?.Start();
        _editVersion++;
        SavedIndicator.Text = "Saving…"; SavedIndicator.Opacity = 1; CommandManager.InvalidateRequerySuggested();
    }
    public bool FlushDocument()
    {
        _captureTimer?.Stop();
        if (!_documentDirty || _isSerializing || _isInitializing) return true;
        try
        {
            var snapshot = _documentCodec.Capture(ContentRichTextBox.Document);
            NoteModel.RtfText = snapshot.Rtf; NoteModel.Text = snapshot.Text;
            NoteModel.Images = snapshot.Images.ToList(); NoteModel.UpdatedAt = DateTime.Now;
            _documentDirty = false; RecordHistory(snapshot); NoteChanged?.Invoke(this, EventArgs.Empty); return true;
        }
        catch (Exception ex)
        { SavedIndicator.Text = "Unsaved — " + ex.Message; SavedIndicator.Opacity = 1; App.Trace("Document save failed: " + ex.Message); return false; }
    }
    public void DiscardPendingChanges() { _documentDirty = false; _captureTimer?.Stop(); }
    private void RecordHistory(DocumentSnapshot snapshot)
    {
        var doc = ContentRichTextBox.Document;
        _history.Record(new(snapshot, doc.ContentStart.GetOffsetToPosition(ContentRichTextBox.Selection.Start), doc.ContentStart.GetOffsetToPosition(ContentRichTextBox.Selection.End)));
        CommandManager.InvalidateRequerySuggested();
    }
    private void RestoreHistory(bool redo)
    {
        if (NoteModel.IsLocked || !FlushDocument()) return;
        var state = redo ? _history.Redo() : _history.Undo(); if (state == null) return;
        _isSerializing = true;
        try
        {
            ContentRichTextBox.Document = _documentCodec.Load(state.Document.Rtf, CreateImage);
            ApplyBackgroundColor(NoteModel.BackgroundColor);
            RestoreImageEditorSelection(state);
            NoteModel.RtfText = state.Document.Rtf; NoteModel.Text = state.Document.Text;
            NoteModel.Images = state.Document.Images.ToList(); NoteModel.UpdatedAt = DateTime.Now;
        }
        finally { _isSerializing = false; }
        NoteChanged?.Invoke(this, EventArgs.Empty); CommandManager.InvalidateRequerySuggested();
    }

    private void RestoreImageEditorSelection(EditorCheckpoint state)
    {
        var document = ContentRichTextBox.Document;
        var start = document.ContentStart.GetPositionAtOffset(state.SelectionStart) ?? document.ContentEnd;
        var end = document.ContentStart.GetPositionAtOffset(state.SelectionEnd) ?? document.ContentEnd;
        ContentRichTextBox.Selection.Select(start, end);
    }
    public void NotifySaveResult(bool success, string? error = null)
    {
        if (_closed) return;
        if (!success) { SavedIndicator.Text = "Unsaved — " + error; SavedIndicator.Opacity = 1; }
        else if (!_documentDirty) ShowSavedIndicator("Saved");
    }

    private async void InsertImageBitmap(BitmapSource bitmap)
    {
        if (NoteModel.IsLocked || !FlushDocument()) return;
        try { ImageAssetStore.ValidateSize(bitmap); }
        catch (Exception ex) { System.Windows.MessageBox.Show(this, ex.Message, "Image import"); return; }
        if (!bitmap.IsFrozen) { bitmap = bitmap.Clone(); bitmap.Freeze(); }
        await ImportImagesAsync(new[] { bitmap }, Array.Empty<string>());
    }
    private async void InsertImageFile(string path) => await ImportImagesAsync(Array.Empty<BitmapSource>(), new[] { path });
    private async Task ImportImagesAsync(IEnumerable<BitmapSource> bitmaps, IEnumerable<string> files)
    {
        if (_closed || NoteModel.IsLocked || _importing || !FlushDocument()) return;
        _importing = true;
        var anchor = ContentRichTextBox.Selection.Start; var selectionEnd = ContentRichTextBox.Selection.End;
        var document = ContentRichTextBox.Document; var store = Assets;
        long startVersion = _editVersion;
        var references = new List<NoteImageReference>(); var errors = new List<string>();
        SavedIndicator.Text = "Importing image…"; SavedIndicator.Opacity = 1;
        foreach (var bitmap in bitmaps)
        { try { references.Add(await store.ImportAsync(NoteModel.Id, bitmap)); } catch (Exception ex) { errors.Add(ex.Message); } }
        foreach (var file in files)
        { try { references.Add(await store.ImportAsync(NoteModel.Id, file)); } catch (Exception ex) { errors.Add(Path.GetFileName(file) + ": " + ex.Message); } }
        _importing = false;
        if (_closed || NoteModel.IsLocked || document != ContentRichTextBox.Document ||
            !string.Equals(Path.GetFullPath(NoteStorageService.GetEffectiveStoragePath()), store.StorageRoot, StringComparison.OrdinalIgnoreCase)) return;
        if (references.Count > 0)
        {
            if (!FlushDocument()) return;
            _isSerializing = true;
            try
            {
                if (_editVersion == startVersion) new TextRange(anchor, selectionEnd).Text = "";
                foreach (var reference in references)
                { var container = new InlineUIContainer(CreateImage(reference), anchor); anchor = container.ElementEnd; }
                ContentRichTextBox.CaretPosition = anchor;
            }
            finally { _isSerializing = false; }
            _documentDirty = true; FlushDocument(); ContentRichTextBox.Focus();
        }
        if (errors.Count > 0) System.Windows.MessageBox.Show(this, string.Join("\n", errors), "Image import", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
    private void ContentRichTextBox_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged) UpdateImageWidths(refreshThumbnails: !_isResizing);
    }

    private void UpdateImageWidths(bool refreshThumbnails)
    {
        foreach (var image in NoteDocumentCodec.Images(ContentRichTextBox.Document))
            image.UpdateResponsiveWidth(EditorWidth, refreshThumbnails);
    }
    private void ContentRichTextBox_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = !NoteModel.IsLocked && e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] files && files.Any(IsImageFile)
            ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)) e.Handled = true;
    }
    private async void ContentRichTextBox_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (NoteModel.IsLocked || e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] files) return;
        e.Handled = true;
        var pointer = ContentRichTextBox.GetPositionFromPoint(e.GetPosition(ContentRichTextBox), true);
        if (pointer != null) ContentRichTextBox.Selection.Select(pointer, pointer);
        await ImportImagesAsync(Array.Empty<BitmapSource>(), files.Where(IsImageFile));
    }
    private void ContentRichTextBox_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source || FindVisualParent<NoteImageControl>(source) is not { } image) return;
        e.Handled = true;
        ShowImageActionPopup(image);
    }
    private void NoteImageControl_DoubleClicked(object? sender, EventArgs e) { if (sender is NoteImageControl image) OpenImageViewer(image); }
    private void OpenImageViewer(NoteImageControl image)
    {
        ImageActionPopup.IsOpen = false;
        if (File.Exists(image.FullPath)) new ImageViewerWindow(image.FullPath) { Owner = this }.Show();
        else System.Windows.MessageBox.Show(this, "The image file is unavailable. Its reference is preserved. Restore the file and reopen the note to retry.", "Image unavailable");
    }
    private void ShowImageActionPopup(NoteImageControl image)
    {
        HyperlinkActionPopup.IsOpen = false; FormatPopup.IsOpen = false;
        ImageActionPopup.IsOpen = false;
        _activeImageControl = image;
        bool hasFile = File.Exists(image.FullPath);
        foreach (var option in new[] { ImageRatio30Btn, ImageRatio50Btn, ImageRatio75Btn, ImageRatio100Btn })
            option.IsEnabled = !NoteModel.IsLocked;
        UpdateImageWidthSelection(image.WidthRatio);
        DeleteImageBtn.IsEnabled = !NoteModel.IsLocked;
        ViewImageFullBtn.IsEnabled = CopyImageBtn.IsEnabled = SaveImageAsBtn.IsEnabled = SaveOriginalImageBtn.IsEnabled = hasFile;
        RelinkImageBtn.IsEnabled = !NoteModel.IsLocked && !_importing;
        ImageActionPopup.PlacementTarget = image;
        ImageActionPopup.IsOpen = true;
    }

    private void UpdateImageWidthSelection(double ratio)
    {
        ImageRatio30Btn.IsChecked = Math.Abs(ratio - .30) < .005;
        ImageRatio50Btn.IsChecked = Math.Abs(ratio - .50) < .005;
        ImageRatio75Btn.IsChecked = Math.Abs(ratio - .75) < .005;
        ImageRatio100Btn.IsChecked = Math.Abs(ratio - 1.00) < .005;
    }
    private void RetryImageBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_activeImageControl is not { } image) return;
        ImageActionPopup.IsOpen = false;
        image.LoadReference(Assets.Resolve(NoteModel.Id, image.FileName), image.Reference, EditorWidth);
    }
    private async void RelinkImageBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_activeImageControl is not { } image) return;
        ImageActionPopup.IsOpen = false;
        await RelinkImageAsync(image);
    }
    private void SaveOriginalImageBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_activeImageControl is not { } image) return;
        ImageActionPopup.IsOpen = false;
        string extension = Path.GetExtension(image.FullPath);
        var dialog = new Microsoft.Win32.SaveFileDialog { FileName = "Image" + extension, DefaultExt = extension, Filter = "Original image|*" + extension };
        if (dialog.ShowDialog(this) != true) return;
        try { ImageExportService.SaveOriginal(image.FullPath, dialog.FileName); }
        catch (Exception ex) { System.Windows.MessageBox.Show(this, ex.Message, "Save original image"); }
    }
    private void ImageSizePreset_Click(object sender, RoutedEventArgs e)
    {
        if (_activeImageControl is not { } image) return;
        if (!NoteModel.IsLocked && FlushDocument() &&
            sender is FrameworkElement { Tag: string ratio } && double.TryParse(ratio,
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var width))
            image.SetWidthRatio(width, EditorWidth);
        UpdateImageWidthSelection(image.WidthRatio);
    }
    private void OpenFullSizeImageBtn_Click(object sender, RoutedEventArgs e) { if (_activeImageControl != null) OpenImageViewer(_activeImageControl); }
    private async void CopyImageBtn_Click(object sender, RoutedEventArgs e)
    {
        var image = _activeImageControl; if (image == null) return;
        ImageActionPopup.IsOpen = false;
        try
        {
            var bitmap = await ImageDecodeService.LoadAsync(image.FullPath, 0, CancellationToken.None);
            var data = new System.Windows.DataObject(); data.SetImage(bitmap);
            data.SetFileDropList(new System.Collections.Specialized.StringCollection { image.FullPath });
            System.Windows.Clipboard.SetDataObject(data, true); ShowSavedIndicator("Image copied");
        }
        catch (Exception ex) { System.Windows.MessageBox.Show(this, ex.Message, "Copy image"); }
    }
    private void SaveImageAsBtn_Click(object sender, RoutedEventArgs e)
    {
        var image = _activeImageControl; if (image == null) return;
        ImageActionPopup.IsOpen = false;
        var dialog = new Microsoft.Win32.SaveFileDialog { FileName = "Image.png", Filter = "PNG Image|*.png|JPEG Image|*.jpg", DefaultExt = ".png" };
        if (dialog.ShowDialog(this) != true) return;
        try { ImageExportService.Export(image.FullPath, dialog.FileName); ShowSavedIndicator("Image exported"); }
        catch (Exception ex) { System.Windows.MessageBox.Show(this, ex.Message, "Export image"); }
    }
    private void DeleteImageBtn_Click(object sender, RoutedEventArgs e)
    {
        if (NoteModel.IsLocked || _activeImageControl == null || !FlushDocument()) return;
        var control = _activeImageControl; ImageActionPopup.IsOpen = false;
        if (control.Parent is InlineUIContainer container) new TextRange(container.ElementStart, container.ElementEnd).Text = "";
        _activeImageControl = null; _documentDirty = true; FlushDocument();
    }
}
