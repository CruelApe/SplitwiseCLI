using SplitwiseCLI.Configuration;
using Xunit;

namespace SplitwiseCLI.Tests;

public class UserConfigStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"SplitwiseCLI.Tests-config-{Guid.NewGuid():N}.json");
    private readonly UserConfigStore _store;

    public UserConfigStoreTests() => _store = new UserConfigStore(_path);

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    [Fact]
    public void LoadApiKey_ReturnsNull_WhenNoConfigFileExists()
    {
        Assert.Null(_store.LoadApiKey());
    }

    [Fact]
    public void SaveApiKey_ThenLoadApiKey_RoundTrips()
    {
        _store.SaveApiKey("super-secret-key");

        Assert.Equal("super-secret-key", _store.LoadApiKey());
    }

    [Fact]
    public void SaveApiKey_DoesNotStorePlaintextOnDisk()
    {
        _store.SaveApiKey("super-secret-key");

        var raw = File.ReadAllText(_path);

        Assert.DoesNotContain("super-secret-key", raw);
    }

    [Fact]
    public void SaveApiKey_CreatesConfigDirectory_WhenMissing()
    {
        var nestedPath = Path.Combine(Path.GetTempPath(), $"SplitwiseCLI.Tests-{Guid.NewGuid():N}", "nested", "config.json");
        var store = new UserConfigStore(nestedPath);

        store.SaveApiKey("key");

        Assert.True(File.Exists(nestedPath));
        Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(nestedPath))!, recursive: true);
    }

    [Fact]
    public void Clear_RemovesConfigFile()
    {
        _store.SaveApiKey("super-secret-key");

        _store.Clear();

        Assert.Null(_store.LoadApiKey());
        Assert.False(File.Exists(_path));
    }

    [Fact]
    public void Clear_WhenNoFileExists_DoesNotThrow()
    {
        _store.Clear();
    }
}
