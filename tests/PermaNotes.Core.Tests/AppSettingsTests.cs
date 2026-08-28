using System;
using System.IO;
using System.Text.Json;
using PermaNotes.Core.Models;

namespace PermaNotes.Core.Tests;

public class AppSettingsTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _primarySettingsFile;
    private readonly string _legacySettingsFile;

    public AppSettingsTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "PermaNotes_SettingsTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _primarySettingsFile = Path.Combine(_testDir, "PermaNotes", "settings.json");
        _legacySettingsFile = Path.Combine(_testDir, "DesktopNotes", "settings.json");
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenNoSettingsFilesExist()
    {
        var settings = AppSettings.Load(_primarySettingsFile, _legacySettingsFile);

        Assert.NotNull(settings);
        Assert.True(settings.ConfirmBeforeDelete);
        Assert.Equal(1.0, settings.DefaultOpacity);
        Assert.Equal("#FFF9C4", settings.DefaultNoteColor);
        Assert.Empty(settings.CustomStoragePath);
    }

    [Fact]
    public void Load_LoadsPrimarySettings_WhenPrimaryExists()
    {
        var dir = Path.GetDirectoryName(_primarySettingsFile)!;
        Directory.CreateDirectory(dir);
        var expected = new AppSettings
        {
            ConfirmBeforeDelete = false,
            DefaultOpacity = 0.75,
            DefaultNoteColor = "#E1BEE7",
            CustomStoragePath = "C:\\CustomPath"
        };
        File.WriteAllText(_primarySettingsFile, JsonSerializer.Serialize(expected));

        var loaded = AppSettings.Load(_primarySettingsFile, _legacySettingsFile);

        Assert.False(loaded.ConfirmBeforeDelete);
        Assert.Equal(0.75, loaded.DefaultOpacity);
        Assert.Equal("#E1BEE7", loaded.DefaultNoteColor);
        Assert.Equal("C:\\CustomPath", loaded.CustomStoragePath);
    }

    [Fact]
    public void Load_MigratesLegacySettings_WhenOnlyLegacyExists()
    {
        var legacyDir = Path.GetDirectoryName(_legacySettingsFile)!;
        Directory.CreateDirectory(legacyDir);
        var legacySettings = new AppSettings
        {
            ConfirmBeforeDelete = false,
            DefaultOpacity = 0.85,
            DefaultNoteColor = "#B2DFDB",
            CustomStoragePath = "D:\\LegacyNotes"
        };
        File.WriteAllText(_legacySettingsFile, JsonSerializer.Serialize(legacySettings));

        Assert.False(File.Exists(_primarySettingsFile));

        var loaded = AppSettings.Load(_primarySettingsFile, _legacySettingsFile);

        // Should load legacy values
        Assert.False(loaded.ConfirmBeforeDelete);
        Assert.Equal(0.85, loaded.DefaultOpacity);
        Assert.Equal("#B2DFDB", loaded.DefaultNoteColor);
        Assert.Equal("D:\\LegacyNotes", loaded.CustomStoragePath);

        // Should have migrated and saved forward to primary file
        Assert.True(File.Exists(_primarySettingsFile));
        var migratedJson = File.ReadAllText(_primarySettingsFile);
        var reloadedFromPrimary = JsonSerializer.Deserialize<AppSettings>(migratedJson);
        Assert.NotNull(reloadedFromPrimary);
        Assert.Equal(0.85, reloadedFromPrimary.DefaultOpacity);
        Assert.Equal("#B2DFDB", reloadedFromPrimary.DefaultNoteColor);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }
}
