namespace DesktopNotes.Services;

public sealed record EditorCheckpoint(DocumentSnapshot Document, int SelectionStart, int SelectionEnd);

/// <summary>One history for text, formatting and images; snapshots hold references, never bitmaps.</summary>
public sealed class NoteEditorHistory : IDisposable
{
    private const long Budget = 16 * 1024 * 1024;
    private static readonly List<WeakReference<NoteEditorHistory>> Sessions = new();
    private readonly List<EditorCheckpoint> _states = new();
    private int _position = -1;
    private long _stamp;
    public bool CanUndo => _position > 0;
    public bool CanRedo => _position >= 0 && _position < _states.Count - 1;
    internal EditorCheckpoint? Current => _position >= 0 ? _states[_position] : null;
    public NoteEditorHistory() => Sessions.Add(new(this));

    public void Record(EditorCheckpoint state)
    {
        if (_position >= 0 && _states[_position].Document.Rtf == state.Document.Rtf) return;
        if (_position < _states.Count - 1) _states.RemoveRange(_position + 1, _states.Count - _position - 1);
        _states.Add(state); _position = _states.Count - 1; _stamp = Environment.TickCount64;
        while (_states.Count > 50) DropOldest();
        EnforceBudget();
    }
    public EditorCheckpoint? Undo() => CanUndo ? _states[--_position] : null;
    public EditorCheckpoint? Redo() => CanRedo ? _states[++_position] : null;

    internal void UpdateSelection(int start, int end)
    {
        if (Current is { } state)
            _states[_position] = state with { SelectionStart = start, SelectionEnd = end };
    }
    private long Bytes => _states.Sum(s => (long)(s.Document.Rtf.Length + s.Document.Text.Length) * 2);
    private void DropOldest()
    {
        if (_states.Count <= 1) return;
        if (_position > 0) { _states.RemoveAt(0); _position--; }
        else _states.RemoveAt(_states.Count - 1);
    }
    private static void EnforceBudget()
    {
        Sessions.RemoveAll(w => !w.TryGetTarget(out _));
        var live = Sessions.Select(w => { w.TryGetTarget(out var h); return h!; }).ToArray();
        while (live.Sum(h => h.Bytes) > Budget)
        {
            var oldest = live.Where(h => h._states.Count > 1).OrderBy(h => h._stamp).FirstOrDefault();
            if (oldest == null) break; // One current checkpoint is required per active document.
            oldest.DropOldest();
        }
    }
    public void Dispose() { _states.Clear(); Sessions.RemoveAll(w => !w.TryGetTarget(out var h) || h == this); }
}
