using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using PermaNotes.Core.Models;

namespace PermaNotes.Core.Storage
{
    public class NoteStorageService
    {
        private string _storageDir = "";
        private string _indexFilePath = "";
        private string _tempIndexFilePath = "";
        private string _notesDir = "";

        private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
        private System.Threading.Timer? _debounceTimer;
        private readonly object _lockObj = new();

        public NoteStorageService(string? customStoragePath = null)
        {
            InitializePaths(customStoragePath ?? GetEffectiveStoragePath());
            MigrateFromOldJsonIfNeeded();
        }

        private string GetEffectiveStoragePath()
        {
            var path = AppSettings.Load().CustomStoragePath;
            if (string.IsNullOrWhiteSpace(path))
            {
#if DEBUG
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PermaNotes_Dev");
#else
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PermaNotes");
#endif
            }
            return path;
        }

        private void InitializePaths(string storageDir)
        {
            _storageDir = storageDir;
            _indexFilePath = Path.Combine(_storageDir, "index.json");
            _tempIndexFilePath = Path.Combine(_storageDir, "index.tmp");
            _notesDir = Path.Combine(_storageDir, "Notes");

            if (!Directory.Exists(_storageDir)) Directory.CreateDirectory(_storageDir);
            if (!Directory.Exists(_notesDir)) Directory.CreateDirectory(_notesDir);
        }
        
        private void MigrateFromOldJsonIfNeeded()
        {
            var oldNotesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopNotes", "notes.json");
            if (File.Exists(oldNotesPath) && !File.Exists(_indexFilePath))
            {
                try
                {
                    var json = File.ReadAllText(oldNotesPath);
                    var oldNotes = JsonSerializer.Deserialize<List<Note>>(json, _jsonOptions);
                    if (oldNotes != null)
                    {
                        SaveNotesImmediateInternal(oldNotes);
                        File.Move(oldNotesPath, oldNotesPath + ".migrated");
                    }
                }
                catch { }
            }
        }

        public void ChangeStorageDirectory(string newPath)
        {
            lock (_lockObj)
            {
                if (string.IsNullOrWhiteSpace(newPath) || newPath.Equals(_storageDir, StringComparison.OrdinalIgnoreCase))
                    return;

                if (!Directory.Exists(newPath)) Directory.CreateDirectory(newPath);

                if (File.Exists(_indexFilePath))
                {
                    File.Copy(_indexFilePath, Path.Combine(newPath, "index.json"), true);
                    File.Delete(_indexFilePath);
                }

                var newNotesDir = Path.Combine(newPath, "Notes");
                if (!Directory.Exists(newNotesDir)) Directory.CreateDirectory(newNotesDir);

                if (Directory.Exists(_notesDir))
                {
                    foreach (var file in Directory.GetFiles(_notesDir, "*.rtf"))
                    {
                        var dest = Path.Combine(newNotesDir, Path.GetFileName(file));
                        File.Copy(file, dest, true);
                        File.Delete(file);
                    }
                    try { Directory.Delete(_notesDir, false); } catch { }
                }

                InitializePaths(newPath);
            }
        }

        private string GetShortId(Guid id) => id.ToString("N").Substring(0, 8);
        
        private string SanitizeTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return "Note";
            var invalids = Path.GetInvalidFileNameChars();
            var sanitized = new string(title.Select(c => invalids.Contains(c) ? '_' : c).ToArray());
            return sanitized.Trim();
        }

        public List<Note> LoadNotes()
        {
            lock (_lockObj)
            {
                var loadedNotes = new List<Note>();
                var metadataLookup = new Dictionary<string, Note>();

                if (File.Exists(_indexFilePath))
                {
                    try
                    {
                        var json = File.ReadAllText(_indexFilePath);
                        var metadata = JsonSerializer.Deserialize<List<Note>>(json, _jsonOptions);
                        if (metadata != null)
                        {
                            foreach (var n in metadata)
                            {
                                metadataLookup[GetShortId(n.Id)] = n;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to load index.json: {ex.Message}");
                    }
                }

                if (Directory.Exists(_notesDir))
                {
                    var rtfFiles = Directory.GetFiles(_notesDir, "*.rtf");
                    foreach (var file in rtfFiles)
                    {
                        var fileName = Path.GetFileNameWithoutExtension(file);
                        var parts = fileName.Split('_');
                        string shortId = parts.Length > 1 ? parts.Last() : "";
                        
                        Note? note = null;
                        if (!string.IsNullOrEmpty(shortId) && metadataLookup.TryGetValue(shortId, out var existing))
                        {
                            note = existing;
                            metadataLookup.Remove(shortId); 
                            
                            var expectedTitle = string.Join("_", parts.Take(parts.Length - 1));
                            if (!string.IsNullOrWhiteSpace(expectedTitle) && expectedTitle != SanitizeTitle(note.Title))
                            {
                                note.Title = expectedTitle.Replace("_", " ");
                            }
                        }
                        else
                        {
                            note = new Note
                            {
                                Id = Guid.NewGuid(),
                                Title = fileName,
                                X = 100, Y = 100,
                                Width = 200, Height = 150,
                                BackgroundColor = "#FFF9C4",
                                Opacity = 1.0
                            };
                        }

                        try
                        {
                            note.RtfText = File.ReadAllText(file);
                            note.Text = ExtractPlainTextFromRtf(note.RtfText);
                        }
                        catch { }

                        loadedNotes.Add(note);
                    }
                }

                return loadedNotes;
            }
        }

        public void SaveNotesDebounced(List<Note> notes, int delayMs = 500)
        {
            var snapshot = DeepCloneNotes(notes);
            lock (_lockObj)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = new System.Threading.Timer(_ =>
                {
                    SaveNotesImmediateInternal(snapshot);
                }, null, delayMs, Timeout.Infinite);
            }
        }

        public void SaveNotesImmediate(List<Note> notes)
        {
            var snapshot = DeepCloneNotes(notes);
            SaveNotesImmediateInternal(snapshot);
        }

        private void SaveNotesImmediateInternal(List<Note> notes)
        {
            lock (_lockObj)
            {
                try
                {
                    var json = JsonSerializer.Serialize(notes, _jsonOptions);
                    File.WriteAllText(_tempIndexFilePath, json);
                    File.Move(_tempIndexFilePath, _indexFilePath, overwrite: true);

                    var activeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var note in notes)
                    {
                        var shortId = GetShortId(note.Id);
                        var title = SanitizeTitle(note.Title);
                        var fileName = $"{title}_{shortId}.rtf";
                        var filePath = Path.Combine(_notesDir, fileName);

                        activeFiles.Add(fileName);

                        if (!string.IsNullOrEmpty(note.RtfText))
                        {
                            File.WriteAllText(filePath, note.RtfText);
                        }
                    }

                    if (Directory.Exists(_notesDir))
                    {
                        var rtfFiles = Directory.GetFiles(_notesDir, "*.rtf");
                        foreach (var file in rtfFiles)
                        {
                            var name = Path.GetFileName(file);
                            if (!activeFiles.Contains(name))
                            {
                                try { File.Delete(file); } catch { }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error during note save: {ex.Message}");
                }
            }
        }

        private List<Note> DeepCloneNotes(List<Note> notes)
        {
            var cloned = new List<Note>(notes.Count);
            foreach (var n in notes)
            {
                cloned.Add(new Note
                {
                    Id = n.Id,
                    Title = n.Title,
                    Text = n.Text, 
                    RtfText = n.RtfText, 
                    X = n.X,
                    Y = n.Y,
                    Width = n.Width,
                    Height = n.Height,
                    BackgroundColor = n.BackgroundColor,
                    Opacity = n.Opacity,
                    IsAlwaysOnTop = n.IsAlwaysOnTop,
                    IsClosed = n.IsClosed,
                    IsLocked = n.IsLocked,
                    IsClickThrough = n.IsClickThrough,
                    CreatedAt = n.CreatedAt,
                    UpdatedAt = n.UpdatedAt
                });
            }
            return cloned;
        }

        private string ExtractPlainTextFromRtf(string rtf)
        {
            if (string.IsNullOrWhiteSpace(rtf)) return string.Empty;
            try
            {
                string text = System.Text.RegularExpressions.Regex.Replace(rtf, @"\{\\fonttbl[\s\S]*?\}|\{\\colortbl[\s\S]*?\}|\{\\stylesheet[\s\S]*?\}|\{\\\*[\s\S]*?\}", string.Empty);
                text = System.Text.RegularExpressions.Regex.Replace(text, @"\\par|\\line", "\r\n");
                text = text.Replace("\\tab", " ");
                text = System.Text.RegularExpressions.Regex.Replace(text, @"\\u(-?\d+)\??", m =>
                {
                    if (short.TryParse(m.Groups[1].Value, out short code))
                    {
                        return ((char)code).ToString();
                    }
                    return string.Empty;
                });
                text = System.Text.RegularExpressions.Regex.Replace(text, @"\\[a-zA-Z]+\-?\d*\s?", string.Empty);
                text = text.Replace("{", "").Replace("}", "").Trim();
                return text;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
