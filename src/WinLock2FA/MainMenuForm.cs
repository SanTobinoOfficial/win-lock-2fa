using System.Drawing;
using System.Windows.Forms;

namespace WinLock2FA;

/// <summary>Ordinary control-panel window - not a lock screen.</summary>
public class MainMenuForm : Form
{
    private readonly Label _statusLabel;

    public MainMenuForm()
    {
        Text = "WinLock2FA";
        Width = 420;
        Height = 320;
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

        _statusLabel = new Label
        {
            Left = 20,
            Top = 240,
            Width = 380,
            Height = 50,
            ForeColor = Color.DimGray,
            Text = StatusText(),
        };

        Controls.Add(title);
        Controls.Add(setupButton);
        Controls.Add(installButton);
        Controls.Add(uninstallButton);
        Controls.Add(testButton);
        Controls.Add(_statusLabel);
    }

    private string StatusText()
    {
        var count = QuestionStore.Load().Count;
        var installed = Installer.IsInstalled();
        return $"Zapisanych pytań: {count}\nZainstalowane przy logowaniu: {(installed ? "TAK" : "nie")}";
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
