using System.Security.Cryptography;
using System.Text.Json;

namespace SplitwiseCLI.Configuration;

// Persists the Splitwise API key across runs/directories - needed once the app is
// installed as a global tool, since there's no single project directory for a
// ".env" file to live next to anymore. The key is stored encrypted (see SecretProtector).
public sealed class UserConfigStore(string? configFilePath = null)
{
    public static string DefaultConfigFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SplitwiseCLI", "config.json");

    public string ConfigFilePath { get; } = configFilePath ?? DefaultConfigFilePath;

    public string? LoadApiKey()
    {
        var stored = ReadStoredConfig();
        if (stored?.EncryptedApiKey is null)
        {
            return null;
        }

        try
        {
            return SecretProtector.Unprotect(stored.EncryptedApiKey);
        }
        catch (CryptographicException)
        {
            // Can't be decrypted (e.g. moved to a different Windows user account) - treat as absent
            // rather than crashing; the caller will fall through to re-prompting.
            return null;
        }
    }

    public void SaveApiKey(string apiKey)
    {
        var directory = Path.GetDirectoryName(ConfigFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var stored = new StoredUserConfig { EncryptedApiKey = SecretProtector.Protect(apiKey) };
        File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(stored));
    }

    public void Clear()
    {
        if (File.Exists(ConfigFilePath))
        {
            File.Delete(ConfigFilePath);
        }
    }

    private StoredUserConfig? ReadStoredConfig()
    {
        if (!File.Exists(ConfigFilePath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StoredUserConfig>(File.ReadAllText(ConfigFilePath));
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed class StoredUserConfig
{
    public string? EncryptedApiKey { get; set; }
}
