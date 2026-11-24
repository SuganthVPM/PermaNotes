using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using DesktopNotes.Models;
using DesktopNotes.Views;
using Timer = System.Threading.Timer;

namespace DesktopNotes.Services;

public sealed record StorageSaveResult(IReadOnlyList<Note> Notes, bool Success, string? Error, long Generation);

/// <summary>Ordered, atomic per-file saves. Assets are retained for history and recovery.</summary>
public sealed class NoteStorageService : IDisposable
{
    private string _storageDir;
    private static string? _activeRoot;
    private readonly object _gate = new();
    private Timer? _timer;
    private long _generation;
    private readonly Dictionary<string, string> _written = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _managedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly JsonSerializerOptions _readOptions = new() { WriteIndented = true };
    private readonly JsonSerializerOptions _writeOptions;
    public event Action<StorageSaveResult>? SaveCompleted;
    public long Generation => Interlocked.Read(ref _generation);

    public NoteStorageService(string? storageRoot = null)
    {
        _storageDir = Path.GetFullPath(storageRoot ?? GetConfiguredPath());
        _activeRoot = _storageDir;
        Directory.CreateDirectory(Path.Combine(_storageDir, "Notes"));
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            if (info.Type == typeof(Note))
                foreach (var property in info.Properties)
                    if (property.Name == nameof(Note.RtfText)) property.ShouldSerialize = (_, _) => false;
        });
        _writeOptions = new() { WriteIndented = true, TypeInfoResolver = resolver };
        // Keep the old JSON untouched until a successful durable conversion exists.
        string legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopNotes", "notes.json");
#if !DEV
        if (storageRoot == null && !File.Exists(IndexPath) && !File.Exists(IndexPath + ".bak")
            && !Directory.EnumerateFiles(NotesPath, "*.rtf").Any() && File.Exists(legacy))
        {
            var notes = JsonSerializer.Deserialize<List<Note>>(File.ReadAllText(legacy)) ?? new();
            var result = SaveSnapshot(notes, ++_generation);
            if (result?.Success != true) throw new IOException(result?.Error);
        }
#endif
    }
    private string IndexPath => Path.Combine(_storageDir, "index.json");
    private string NotesPath => Path.Combine(_storageDir, "Notes");
    private static string GetConfiguredPath()
    {
        var custom = AppSettings.Load().CustomStoragePath;
        if (!string.IsNullOrWhiteSpace(custom)) return custom;
#if DEV
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PermaNotes_Dev");
#else
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PermaNotes");
#endif
    }
    public static string GetEffectiveStoragePath() => _activeRoot ?? GetConfiguredPath();
    public static string GetShortId(Guid id) => id.ToString("N")[..8];
    private static string FileName(Note note)
    {
        var title = new string(note.Title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).Trim();
        if (string.IsNullOrEmpty(title)) title = "Note";
        return title + "_" + GetShortId(note.Id) + ".rtf";
    }
    public List<Note> LoadNotes()
    {
        lock (_gate)
        {
            var metadata = new List<Note>();
            if (File.Exists(IndexPath) || File.Exists(IndexPath + ".bak"))
            {
                try { metadata = JsonSerializer.Deserialize<List<Note>>(File.ReadAllText(IndexPath), _readOptions) ?? new(); }
                catch (Exception) when (File.Exists(IndexPath + ".bak"))
                { metadata = JsonSerializer.Deserialize<List<Note>>(File.ReadAllText(IndexPath + ".bak"), _readOptions) ?? new(); }
            }
            var lookup = metadata.ToDictionary(n => GetShortId(n.Id), StringComparer.OrdinalIgnoreCase);
            var loaded = new List<Note>();
            foreach (string file in Directory.EnumerateFiles(NotesPath, "*.rtf"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                string id = name.Split('_').Last();
                if (!lookup.Remove(id, out var note)) note = new()
                { Id = RecoverIdentity(file, id), Title = name.Length > id.Length + 1 && Regex.IsMatch(id, "^[a-fA-F0-9]{8}$") ? name[..^(id.Length + 1)].Replace('_', ' ') : name };
                else if (name.Length > id.Length + 1)
                {
                    string title = name[..^(id.Length + 1)];
                    if (title != Path.GetFileNameWithoutExtension(FileName(note))[..^(id.Length + 1)]) note.Title = title.Replace('_', ' ');
                }
                note.ContentFilePath = file;
                note.IsContentLoaded = !note.IsClosed || string.IsNullOrEmpty(note.Text);
                if (note.IsContentLoaded) LoadContent(note);
                else note.RtfText = "";
                loaded.Add(note);
                _managedPaths.Add(file);
            }
            // Preserve indexed notes whose RTF disappeared; recover from backup/index rather than silently dropping them.
            foreach (var note in lookup.Values)
            {
                string path = Path.Combine(NotesPath, FileName(note));
                note.ContentFilePath = path;
                if (File.Exists(path + ".bak")) note.RtfText = File.ReadAllText(path + ".bak");
                else
                {
                    string trash = Path.Combine(_storageDir, "Trash");
                    string? retired = Directory.Exists(trash) ? StorageTree.Files(trash)
                        .Where(f => Path.GetFileName(f).Contains("_" + GetShortId(note.Id) + ".rtf", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() : null;
                    if (retired != null) note.RtfText = File.ReadAllText(retired);
                }
                note.IsContentLoaded = true;
                loaded.Add(note);
            }
            return loaded;
        }
    }
    public void EnsureContentLoaded(Note note)
    { lock (_gate) { if (!note.IsContentLoaded) LoadContent(note); } }
    private void LoadContent(Note note)
    {
        if (note.ContentFilePath != null)
        {
            try { note.RtfText = ReadRtf(note.ContentFilePath); }
            catch (Exception) when (File.Exists(note.ContentFilePath + ".bak"))
            {
                note.RtfText = ReadRtf(note.ContentFilePath + ".bak");
                if (File.Exists(note.ContentFilePath))
                {
                    string recovery = Path.Combine(_storageDir, "Recovery", Guid.NewGuid().ToString("N") + "_" + Path.GetFileName(note.ContentFilePath));
                    Directory.CreateDirectory(Path.GetDirectoryName(recovery)!); File.Move(note.ContentFilePath, recovery);
                }
            }
            _written[note.ContentFilePath] = ContentHash(note.RtfText);
        }
        note.IsContentLoaded = true;
        if (string.IsNullOrEmpty(note.Text)) note.Text = NoteDocumentCodec.ExtractPlainText(note.RtfText);
    }
    public void SaveNotesDebounced(List<Note> notes, int delayMs = 500)
    {
        var snapshot = Clone(notes);
        lock (_gate)
        {
            long generation = ++_generation; _timer?.Dispose();
            _timer = new Timer(_ =>
            {
                var result = SaveSnapshot(snapshot, generation);
                if (result != null) SaveCompleted?.Invoke(result);
            }, null, delayMs, Timeout.Infinite);
        }
    }
    public bool SaveNotesImmediate(List<Note> notes)
    {
        StorageSaveResult? result;
        lock (_gate) { _timer?.Dispose(); result = SaveSnapshot(Clone(notes), ++_generation); }
        if (result != null) SaveCompleted?.Invoke(result);
        return result?.Success == true;
    }
    private StorageSaveResult? SaveSnapshot(List<Note> notes, long generation)
    {
        lock (_gate)
        {
            if (generation != _generation) return null;
            try
            {
                StorageMigrationService.MarkDestinationInUse(_storageDir);
                // Commit content before its metadata. Preserve old content and assets for recovery.
                var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (notes.Select(n => GetShortId(n.Id)).Distinct(StringComparer.OrdinalIgnoreCase).Count() != notes.Count)
                    throw new InvalidDataException("Two notes share the same storage identifier. Their files were preserved.");
                foreach (var note in notes)
                {
                    string path = Path.Combine(NotesPath, FileName(note)); active.Add(path);
                    if (!note.IsContentLoaded)
                    {
                        if (note.ContentFilePath != null && !string.Equals(path, note.ContentFilePath, StringComparison.OrdinalIgnoreCase))
                        {
                            AtomicFile.Write(path, output => { using var input = File.OpenRead(note.ContentFilePath); input.CopyTo(output); });
                            _managedPaths.Add(path);
                        }
                        continue;
                    }
                    string rtf = WithIdentity(string.IsNullOrEmpty(note.RtfText) ? PlainRtf(note.Text) : note.RtfText, note.Id);
                    string hash = ContentHash(rtf);
                    if (!_written.TryGetValue(path, out var previous) || previous != hash || !File.Exists(path))
                    { AtomicFile.WriteText(path, rtf); _written[path] = hash; }
                    _managedPaths.Add(path);
                }
                // Retire obsolete names before publishing metadata, so a committed deletion
                // cannot resurrect from a leftover RTF. Previous metadata can recover retired RTFs.
                foreach (string file in _managedPaths.ToArray())
                {
                    if (active.Contains(file)) continue;
                    if (!File.Exists(file)) { _managedPaths.Remove(file); continue; }
                    string retired = Path.Combine(_storageDir, "Trash", DateTime.UtcNow.ToString("yyyyMMdd"), Path.GetFileName(file) + "." + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(Path.GetDirectoryName(retired)!);
                    File.Move(file, retired); _written.Remove(file);
                    _managedPaths.Remove(file);
                    File.SetLastWriteTimeUtc(retired, DateTime.UtcNow);
                }
                AtomicFile.Write(IndexPath, output => JsonSerializer.Serialize(output, notes, _writeOptions));
                return new(notes, true, null, generation);
            }
            catch (Exception ex) { return new(notes, false, ex.Message, generation); }
        }
    }
    public void PrepareStorageMigration(string newPath)
    {
        lock (_gate)
        {
            string full = Path.GetFullPath(newPath);
            if (string.Equals(full, _storageDir, StringComparison.OrdinalIgnoreCase)) return;
            _timer?.Dispose(); ++_generation;
            StorageMigrationService.CopyVerified(_storageDir, full);
        }
    }
    public void ActivateStorageDirectory(string newPath)
    { lock (_gate) { _storageDir = Path.GetFullPath(newPath); _activeRoot = _storageDir; _written.Clear(); _managedPaths.Clear(); } }
    private static List<Note> Clone(IEnumerable<Note> notes) => notes.Select(n => new Note
    {
        Id = n.Id, Title = n.Title, Text = n.Text, RtfText = n.RtfText, IsContentLoaded = n.IsContentLoaded, ContentFilePath = n.ContentFilePath,
        X = n.X, Y = n.Y, Width = n.Width, Height = n.Height, BackgroundColor = n.BackgroundColor, Opacity = n.Opacity,
        IsAlwaysOnTop = n.IsAlwaysOnTop, IsClosed = n.IsClosed, IsLocked = n.IsLocked, IsClickThrough = n.IsClickThrough,
        CreatedAt = n.CreatedAt, UpdatedAt = n.UpdatedAt, ReminderAt = n.ReminderAt, ReminderText = n.ReminderText,
        ReminderToastTag = n.ReminderToastTag, ReminderFired = n.ReminderFired, IsHiddenFromCapture = n.IsHiddenFromCapture,
        Images = new(n.Images)
    }).ToList();
    private static string ContentHash(string content) => Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)));
    internal static string ReadRtf(string path)
    {
        string rtf = File.ReadAllText(path);
        if (!rtf.TrimStart('\uFEFF', ' ', '\t', '\r', '\n').StartsWith("{\\rtf", StringComparison.Ordinal))
            throw new InvalidDataException("Invalid note content: " + Path.GetFileName(path));
        int depth = 0;
        for (int i = 0; i < rtf.Length; i++)
        {
            if (rtf[i] == '\\') { i++; continue; }
            if (rtf[i] == '{') depth++;
            else if (rtf[i] == '}' && --depth < 0) throw new InvalidDataException("Unbalanced RTF note.");
        }
        if (depth != 0) throw new InvalidDataException("Incomplete RTF note.");
        return rtf;
    }
    private static string WithIdentity(string rtf, Guid id)
    {
        rtf = rtf.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        rtf = Regex.Replace(rtf, @"\{\\\*\\permanotesid [a-fA-F0-9]{32}\}", "");
        var header = Regex.Match(rtf, @"^\{\\rtf\d+");
        if (!header.Success) throw new InvalidDataException("Invalid RTF content; previous saved note was retained.");
        return rtf.Insert(header.Length, "{\\*\\permanotesid " + id.ToString("N") + "}");
    }
    private static string PlainRtf(string text)
    {
        var rtf = new System.Text.StringBuilder("{\\rtf1\\ansi\\uc1 ");
        foreach (char character in NoteDocumentCodec.EscapeLiteral(text).Replace("\r\n", "\n").Replace('\r', '\n'))
        {
            if (character == '\n') rtf.Append("\\par ");
            else if (character is '\\' or '{' or '}') rtf.Append('\\').Append(character);
            else if (character is >= ' ' and <= '~') rtf.Append(character);
            else rtf.Append("\\u").Append(unchecked((short)character).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('?');
        }
        return rtf.Append('}').ToString();
    }
    private Guid RecoverIdentity(string file, string shortId)
    {
        using var reader = new StreamReader(file);
        var header = new char[4096]; int count = reader.Read(header, 0, header.Length);
        var marker = Regex.Match(new string(header, 0, count), @"\{\\\*\\permanotesid (?<id>[a-fA-F0-9]{32})\}");
        if (marker.Success && Guid.TryParseExact(marker.Groups["id"].Value, "N", out var restored)) return restored;
        if (Regex.IsMatch(shortId, "^[a-fA-F0-9]{8}$"))
        {
            string images = Path.Combine(_storageDir, "Images");
            if (Directory.Exists(images))
            {
                var matching = Directory.GetDirectories(images).Select(Path.GetFileName)
                    .Where(n => n != null && n.StartsWith(shortId, StringComparison.OrdinalIgnoreCase) && n.Length == 32).ToArray();
                if (matching.Length == 1 && Guid.TryParseExact(matching[0], "N", out var assetOwner)) return assetOwner;
            }
            return Guid.ParseExact(shortId + Guid.NewGuid().ToString("N")[8..], "N");
        }
        return Guid.NewGuid();
    }
    public void Dispose() { lock (_gate) { _timer?.Dispose(); ++_generation; } }
}
