using System;

namespace DesktopNotes.Models
{
    /// <summary>
    /// Represents a single sticky note and its full persisted state.
    /// Instances are serialized to JSON by <see cref="Services.NoteStorageService"/>
    /// and restored on next launch.
    ///
    /// This is a plain data class with no logic — all behavior lives in the view layer
    /// (<see cref="Views.NoteWindow"/>) and the application layer (<see cref="App"/>).
    /// </summary>
    public class Note
    {
        /// <summary>Stable unique identifier. Generated once on creation; never changes.</summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// User-editable note title shown in the header bar.
        /// Defaults to "Untitled Note"; can be changed by double-clicking the title.
        /// </summary>
        public string Title { get; set; } = "Untitled Note";

        /// <summary>
        /// Plain-text fallback content extracted from the document.
        /// Kept for search/preview purposes; the canonical content is <see cref="RtfText"/>.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Full RTF-encoded document content, base-64-safe UTF-8 string.
        /// Preserves all rich formatting: bold, italic, underline, strikethrough,
        /// highlight, custom font sizes, bullet lists, etc.
        /// </summary>
        public string RtfText { get; set; } = string.Empty;

        /// <summary>Window left edge position in screen pixels.</summary>
        public double X { get; set; } = 200;

        /// <summary>Window top edge position in screen pixels.</summary>
        public double Y { get; set; } = 200;

        /// <summary>Window width in pixels.</summary>
        public double Width { get; set; } = 320;

        /// <summary>Window height in pixels.</summary>
        public double Height { get; set; } = 280;

        /// <summary>
        /// Background colour as an HTML hex string (e.g. "#FFF9C4").
        /// Defaults to soft sticky-note yellow.
        /// </summary>
        public string BackgroundColor { get; set; } = "#FFF9C4";

        /// <summary>
        /// Overall window opacity in the range [0.0, 1.0].
        /// Applied to the entire note window; also modulates the border alpha.
        /// </summary>
        public double Opacity { get; set; } = 1.0;

        /// <summary>
        /// When <c>true</c> the note window floats above all other windows (Topmost=true)
        /// and is detached from the desktop WorkerW owner.
        /// </summary>
        public bool IsAlwaysOnTop { get; set; } = false;

        /// <summary>
        /// When <c>true</c> the note is not currently shown on screen.
        /// The model is still stored in <see cref="App._allNotes"/> and can be
        /// reopened via the Note Manager or the tray icon.
        /// </summary>
        public bool IsClosed { get; set; } = false;

        /// <summary>
        /// When <c>true</c> the note content is read-only: the RichTextBox rejects input,
        /// the format popup is suppressed, and text-changed callbacks are short-circuited.
        /// Toggle with the lock button in the header or Ctrl+Shift+L.
        /// </summary>
        public bool IsLocked { get; set; } = false;

        /// <summary>
        /// When <c>true</c> all mouse input passes through the note to the window beneath,
        /// except for the reserved click-through handle region (top-left corner).
        /// Forces <see cref="IsAlwaysOnTop"/> = true when enabled.
        /// Toggle via right-click context menu or by clicking the on-canvas pin handle.
        /// </summary>
        public bool IsClickThrough { get; set; } = false;

        /// <summary>UTC timestamp of when this note was first created.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>UTC timestamp of the last content, position, or metadata change.</summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
