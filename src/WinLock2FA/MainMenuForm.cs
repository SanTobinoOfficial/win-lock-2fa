using System.Drawing;
using System.Windows.Forms;

namespace WinLock2FA;

/// <summary>Ordinary control-panel window - not a lock screen.</summary>
public class MainMenuForm : Form
{
    private readonly Label _statusLabel;
    private readonly Button _toggleButton;
    private readonly Button _expiryToggleButton;

    public MainMenuForm()
    {
        Text = "WinLock2FA";
        Width = 420;
        Height = 440;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;

        var title = new Label
        {
            Text = "WinLock2FA",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            Left = 20,
            Top = 15,
            Width = 380,
            TextAlign = ContentAlignment.MiddleCenter,
        };

        var setupButton = new Button { Text = "1. Ustaw pytania i odpowiedzi", Left = 60, Top = 60, Width = 280, Height = 36 };
        setupButton.Click += (_, _) => new SetupForm().ShowDialog(this);

        var installButton = new Button { Text = "2. Zainstaluj (uruchamiaj po zalogowaniu)", Left = 60, Top = 104, Width = 280, Height = 36 };
        installButton.Click += (_, _) => DoInstall();

        var uninstallButton = new Button { Text = "3. Odinstaluj", Left = 60, Top = 148, Width = 280, Height = 36 };
        uninstallButton.Click += (_, _) => DoUninstall();

        var testButton = new Button { Text = "4. Testuj blokadę teraz", Left = 60, Top = 192, Width = 280, Height = 36 };
        testButton.Click += (_, _) => DoTest();

        _toggleButton = new Button { Text = "5. Ochrona: ...", Left = 60, Top = 236, Width = 280, Height = 36 };
        _toggleButton.Click += (_, _) => DoToggleProtection();

        _expiryToggleButton = new Button { Text = "6. Wygaśnięcie: ...", Left = 60, Top = 280, Width = 280, Height = 36 };
        _expiryToggleButton.Click += (_, _) => DoToggleExpiryAutoUninstall();

        _statusLabel = new Label
        {
            Left = 20,
            Top = 324,
            Width = 380,
            Height = 90,
            ForeColor = Color.DimGray,
            Text = StatusText(),
        };

        Controls.Add(title);
        Controls.Add(setupButton);
        Controls.Add(installButton);
        Controls.Add(uninstallButton);
        Controls.Add(testButton);
        Controls.Add(_toggleButton);
        Controls.Add(_expiryToggleButton);
        Controls.Add(_statusLabel);

        RefreshToggleButton();
        RefreshExpiryToggleButton();
    }

    private string StatusText()
    {
        var count = QuestionStore.Load().Count;
        var installed = Installer.IsInstalled();
        var settings = ProtectionSettings.Load();
        return $"Zapisanych pytań: {count}\n" +
               $"Zainstalowane przy logowaniu: {(installed ? "TAK" : "nie")}\n" +
               $"Ochrona: {(settings.Enabled ? "WŁĄCZONA" : "wyłączona")}\n" +
               $"Auto-odinstalowanie {ExpiryPolicy.ExpiryDate:dd.MM.yyyy}: " +
               $"{(settings.ExpiryAutoUninstallEnabled ? "WŁĄCZONE" : "wyłączone")}";
    }

    private void RefreshToggleButton()
    {
        var enabled = ProtectionSettings.Load().Enabled;
        _toggleButton.Text = enabled ? "5. Wyłącz ochronę" : "5. Włącz ochronę";
    }

    private void RefreshExpiryToggleButton()
    {
        var enabled = ProtectionSettings.Load().ExpiryAutoUninstallEnabled;
        _expiryToggleButton.Text = enabled
            ? "6. Wyłącz auto-odinstalowanie"
            : "6. Włącz auto-odinstalowanie";
    }

    private void DoInstall()
    {
        if (QuestionStore.Load().Count < 1)
        {
            MessageBox.Show(this, "Najpierw dodaj co najmniej jedno pytanie (zalecane min. 3).", "Brak pytań", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var (ok, message) = Installer.Install();
        MessageBox.Show(this, message, ok ? "Sukces" : "Błąd", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        _statusLabel.Text = StatusText();
    }

    private void DoUninstall()
    {
        using var codeDialog = new CodePromptDialog("Podaj kod awaryjny, aby wyłączyć ochronę:");
        if (codeDialog.ShowDialog(this) != DialogResult.OK)
            return;

        if (codeDialog.EnteredCode != DebugCode.Value)
        {
            MessageBox.Show(this, "Nieprawidłowy kod.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var (ok, message) = Installer.Uninstall();
        MessageBox.Show(this, message, ok ? "Sukces" : "Błąd", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        _statusLabel.Text = StatusText();
    }

    private void DoToggleProtection()
    {
        var settings = ProtectionSettings.Load();

        if (settings.Enabled)
        {
            // Turning protection off requires the same debug code as
            // Uninstall - it's still "disabling protection", just without
            // touching the scheduled task.
            using var codeDialog = new CodePromptDialog("Podaj kod awaryjny, aby wyłączyć ochronę:");
            if (codeDialog.ShowDialog(this) != DialogResult.OK)
                return;

            if (codeDialog.EnteredCode != DebugCode.Value)
            {
                MessageBox.Show(this, "Nieprawidłowy kod.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            settings.Enabled = false;
            settings.Save();
            MessageBox.Show(this, "Ochrona wyłączona. Blokada nie pojawi się przy kolejnym logowaniu.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            // Turning it back on doesn't need a code - only disabling does.
            settings.Enabled = true;
            settings.Save();
            MessageBox.Show(this, "Ochrona włączona.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        RefreshToggleButton();
        _statusLabel.Text = StatusText();
    }

    private void DoToggleExpiryAutoUninstall()
    {
        // No debug code required either direction: turning this off keeps
        // the lock active past the expiry date (more protection, not less),
        // and turning it back on restores the original one-time behavior.
        var settings = ProtectionSettings.Load();
        settings.ExpiryAutoUninstallEnabled = !settings.ExpiryAutoUninstallEnabled;
        settings.Save();

        MessageBox.Show(
            this,
            settings.ExpiryAutoUninstallEnabled
                ? $"Auto-odinstalowanie {ExpiryPolicy.ExpiryDate:dd.MM.yyyy} włączone."
                : $"Auto-odinstalowanie {ExpiryPolicy.ExpiryDate:dd.MM.yyyy} wyłączone. Blokada będzie działać dalej po tej dacie.",
            "Sukces",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

        RefreshExpiryToggleButton();
        _statusLabel.Text = StatusText();
    }

    private void DoTest()
    {
        var entries = QuestionStore.Load();
        if (entries.Count == 0)
        {
            MessageBox.Show(this, "Najpierw dodaj co najmniej jedno pytanie.", "Brak pytań", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            this,
            "To natychmiast zablokuje ekran pełnoekranowym oknem, tak jak po zalogowaniu.\n\n" +
            "Upewnij się, że pamiętasz przynajmniej jedną odpowiedź. W razie problemów: Ctrl+Alt+Del -> Menedżer zadań -> Zakończ zadanie \"WinLock2FA\".\n\n" +
            "Kontynuować?",
            "Test blokady",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
            return;

        var random = new Random();
        var question = entries[random.Next(entries.Count)];
        Hide();
        using (var lockForm = new LockForm(question))
        {
            // ShowDialog(), not Application.Run() - this app already has a
            // running message loop (this menu's), and WinForms does not
            // support starting a second one on the same thread.
            lockForm.ShowDialog();
        }
        Show();
    }
}
