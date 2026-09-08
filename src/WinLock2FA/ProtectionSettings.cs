using System.Text.Json;

namespace WinLock2FA;

/// <summary>
/// A simple on/off switch, separate from Install/Uninstall: lets the lock
/// be paused from the main menu without touching the HKCU autorun entry.
/// Not secret data (just a boolean), so it is stored as plain JSON, unlike
/// QuestionStore's DPAPI-encrypted answers.
/// </summary>
public class ProtectionSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When true (default), reaching ExpiryPolicy.ExpiryDate makes the app
    /// auto-uninstall itself instead of locking. Turning this off keeps the
    /// lock active past that date - it does not weaken current protection,
    /// so unlike Enabled it is not gated behind the debug code.
    /// </summary>
    public bool ExpiryAutoUninstallEnabled { get; set; } = true;

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
