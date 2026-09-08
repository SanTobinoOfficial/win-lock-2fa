namespace WinLock2FA;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var mode = args.Length > 0 ? args[0].ToLowerInvariant() : "menu";

        switch (mode)
        {
            case "lock":
                RunLock();
                break;

            case "setup":
                Application.Run(new SetupForm());
                break;

            case "install":
                Installer.Install();
                break;

            case "uninstall":
                Installer.Uninstall();
                break;

            default:
                Application.Run(new MainMenuForm());
                break;
        }
    }

    private static void RunLock()
    {
        var entries = QuestionStore.Load();
        if (entries.Count == 0)
        {
            // Nothing configured yet - nothing to enforce. Fail open rather
            // than showing a lock screen that no answer can ever satisfy.
            return;
        }

        var random = new Random();
        var question = entries[random.Next(entries.Count)];
        using var lockForm = new LockForm(question);
        Application.Run(lockForm);
    }
}
