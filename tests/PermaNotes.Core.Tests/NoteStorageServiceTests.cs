using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PermaNotes.Core.Models;
using PermaNotes.Core.Storage;

namespace PermaNotes.Core.Tests;

public class NoteStorageServiceTests : IDisposable
{
    private readonly string _testTempDir;
    private readonly NoteStorageService _service;

    public NoteStorageServiceTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), "PermaNotes_TestStorage_" + Guid.NewGuid().ToString("N"));
        _service = new NoteStorageService(_testTempDir);
    }

    [Fact]
    public void SaveAndLoad_ShouldPersistNotes()
    {
        var notes = new List<Note>
        {
            new Note { Title = "Test Note 1", Text = "Hello", RtfText = @"{\rtf1 Hello}" },
            new Note { Title = "Test Note 2", Text = "World", RtfText = @"{\rtf1 World}" }
        };

        _service.SaveNotesImmediate(notes);

        var loadedNotes = _service.LoadNotes();

        Assert.Equal(2, loadedNotes.Count);
        Assert.Contains(loadedNotes, n => n.Title == "Test Note 1" && n.Text == "Hello");
        Assert.Contains(loadedNotes, n => n.Title == "Test Note 2" && n.Text == "World");
    }

    [Fact]
    public void SaveNotes_WithSpecialCharactersInTitle_ShouldSanitizeFileName()
    {
        var notes = new List<Note>
        {
            new Note { Title = "Test / : * ? < > | Note", Text = "Special chars", RtfText = @"{\rtf1 Special chars}" }
        };

        _service.SaveNotesImmediate(notes);
        var loadedNotes = _service.LoadNotes();

        Assert.Single(loadedNotes);
        Assert.Equal("Special chars", loadedNotes[0].Text);
    }

    [Fact]
    public void ChangeStorageDirectory_ShouldMoveNotesAndIndex()
    {
        var notes = new List<Note>
        {
            new Note { Title = "Relocation Note", Text = "Moving paths", RtfText = @"{\rtf1 Moving}" }
        };

        _service.SaveNotesImmediate(notes);

        var newDir = Path.Combine(Path.GetTempPath(), "PermaNotes_NewStorage_" + Guid.NewGuid().ToString("N"));
        try
        {
            _service.ChangeStorageDirectory(newDir);

            Assert.True(File.Exists(Path.Combine(newDir, "index.json")));
            Assert.True(Directory.Exists(Path.Combine(newDir, "Notes")));

            var reloadedNotes = _service.LoadNotes();
            Assert.Single(reloadedNotes);
            Assert.Equal("Relocation Note", reloadedNotes[0].Title);
        }
        finally
        {
            if (Directory.Exists(newDir))
            {
                try { Directory.Delete(newDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void SaveNotesDebounced_ShouldPersistAfterDelay()
    {
        var notes = new List<Note>
        {
            new Note { Title = "Debounce Note", Text = "Debounced save", RtfText = @"{\rtf1 Debounced}" }
        };

        _service.SaveNotesDebounced(notes, delayMs: 50);

        // Wait for debounce timer to trigger
        System.Threading.Thread.Sleep(150);

        var loadedNotes = _service.LoadNotes();
        Assert.Single(loadedNotes);
        Assert.Equal("Debounce Note", loadedNotes[0].Title);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testTempDir))
        {
            try { Directory.Delete(_testTempDir, true); } catch { }
        }
    }
}
