namespace DesktopNotes.Models;

public sealed record NoteClipboardPayload(string StorageRoot, Guid NoteId, string Rtf);
