using Microsoft.Win32;

namespace WinLock2FA;

/// <summary>
/// Registers/unregisters a per-user autorun entry (HKCU "Run" key) that
/// launches this executable with the "lock" argument whenever the current
/// user logs on.
///
/// Deliberately uses HKCU rather than a Windows Scheduled Task: schtasks
/// /create reliably fails with "Odmowa dostępu" (Access Denied) for local
/// Administrator accounts running non-elevated under UAC - the filtered
/// token carries an explicit deny ACE for the Administrators group, which
/// the Task Scheduler folder's ACL trips on even with /rl limited. HKCU is
/// purely per-user storage and never requires elevation for any account
/// type, admin or standard.
///
/// This keeps the same soft, application-level gate that anyone with the
/// Registry Editor, Task Manager, or Safe Mode access can remove - see
/// README.md for why that's an intentional safety choice.
/// </summary>
public static class Installer
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WinLock2FA";

    public static (bool ok, string message) Install()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
            return (false, "Nie udało się ustalić ścieżki do pliku .exe.");

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null)
                return (false, "Nie udało się otworzyć klucza rejestru HKCU\\...\\Run.");

            key.SetValue(ValueName, $"\"{exePath}\" lock");
            return (true, "Zainstalowano. Blokada uruchomi się automatycznie po każdym Twoim zalogowaniu do Windows.");
        }
        catch (Exception ex)
        {
            return (false, $"Nie udało się zainstalować:\n{ex.Message}");
        }
    }

    public static (bool ok, string message) Uninstall()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
            return (true, "Odinstalowano. Blokada nie będzie się już uruchamiać przy logowaniu.");
        }
        catch (Exception ex)
        {
            return (false, $"Nie udało się odinstalować:\n{ex.Message}");
        }
    }

    public static bool IsInstalled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) != null;
    }
}
