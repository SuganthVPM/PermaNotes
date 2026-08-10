using System;

namespace DesktopNotes.Models
{
    public class Note
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = "Untitled Note";
        public string Text { get; set; } = string.Empty;
        public string RtfText { get; set; } = string.Empty;
        public double X { get; set; } = 200;
        public double Y { get; set; } = 200;
        public double Width { get; set; } = 320;
        public double Height { get; set; } = 280;
        public string BackgroundColor { get; set; } = "#FFF9C4"; // Soft sticky-note yellow
        public double Opacity { get; set; } = 1.0;
        public bool IsAlwaysOnTop { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
