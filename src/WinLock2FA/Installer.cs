using System.Diagnostics;

namespace WinLock2FA;

/// <summary>
/// Registers/unregisters a per-user Scheduled Task that launches this
/// executable with the "lock" argument whenever the current user logs on.
///
/// Deliberately uses a normal (non-admin, "limited") per-user logon trigger
/// instead of touching any system-wide policy, the Windows Shell registry
/// key, or a Credential Provider. That keeps this a soft, application-level
/// gate that anyone with Task Manager or Safe Mode access can remove -
/// see README.md for why that's an intentional safety choice.
/// </summary>
public static class Installer
{
    private const string TaskName = "WinLock2FA";

    public static (bool ok, string message) Install()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
            return (false, "Nie udało się ustalić ścieżki do pliku .exe.");

        var args = $"/create /tn \"{TaskName}\" /tr \"\\\"{exePath}\\\" lock\" /sc onlogon /rl limited /f";
        var (exitCode, output) = RunSchtasks(args);

        return exitCode == 0
            ? (true, "Zainstalowano. Blokada uruchomi się automatycznie po każdym Twoim zalogowaniu do Windows.")
            : (false, $"schtasks zwrócił błąd ({exitCode}):\n{output}");
    }

    public static (bool ok, string message) Uninstall()
    {
        var (exitCode, output) = RunSchtasks($"/delete /tn \"{TaskName}\" /f");
        return exitCode == 0
            ? (true, "Odinstalowano. Blokada nie będzie się już uruchamiać przy logowaniu.")
            : (false, $"schtasks zwrócił błąd ({exitCode}):\n{output}");
    }

    public static bool IsInstalled()
    {
        var (exitCode, _) = RunSchtasks($"/query /tn \"{TaskName}\"");
        return exitCode == 0;
    }

    private static (int exitCode, string output) RunSchtasks(string arguments)
    {
        var psi = new ProcessStartInfo("schtasks.exe", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdout + stderr);
    }
}
