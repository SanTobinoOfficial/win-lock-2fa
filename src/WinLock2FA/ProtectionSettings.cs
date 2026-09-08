using System.Text.Json;

namespace WinLock2FA;

/// <summary>
/// A simple on/off switch, separate from Install/Uninstall: lets the lock
/// be paused from the main menu without touching the scheduled task. Not
/// secret data (just a boolean), so it is stored as plain JSON, unlike
/// QuestionStore's DPAPI-encrypted answers.
/// </summary>
public class ProtectionSettings
{
    public bool Enabled { get; set; } = true;

    private static string FilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinLock2FA",
            "settings.json");

    public static ProtectionSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new ProtectionSettings();

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<ProtectionSettings>(json) ?? new ProtectionSettings();
        }
        catch
        {
            return new ProtectionSettings();
        }
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
    }
}
