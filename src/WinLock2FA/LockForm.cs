using System.Drawing;
using System.Windows.Forms;

namespace WinLock2FA;

/// <summary>
/// Full-screen challenge shown on top of everything else. Picks one random
/// question from the stored set and will not close itself until the correct
/// answer is entered. Installs a low-level keyboard hook to swallow Alt+Tab,
/// the Windows key, Alt+F4 and Ctrl+Esc while it is showing.
///
/// This is a "soft" application-level lock, not a Windows logon replacement.
/// Ctrl+Alt+Del is intentionally never intercepted (it cannot be, and is left
/// as the documented emergency exit via Task Manager). See README.md.
///
/// Also carries its own copy of the ExpiryPolicy check (Program.RunLock
/// already checks it before ever constructing this form): if this form
/// somehow still gets shown past the expiry date, it shows a large
/// explanatory notice instead of a real question - see BuildExpiredUi.
/// </summary>
public class LockForm : Form
{
    private readonly QuestionEntry? _question;
    private readonly Label? _questionLabel;
    private readonly TextBox? _answerBox;
    private readonly Label? _statusLabel;
    private readonly Button? _submitButton;

    private int _failedAttempts;
    private System.Windows.Forms.Timer? _lockoutTimer;
    private int _lockoutSecondsRemaining;

    private nint _hookHandle;
    private NativeMethods.LowLevelKeyboardProc? _hookProc;

    public LockForm(QuestionEntry question)
    {
        _question = question;

        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        StartPosition = FormStartPosition.Manual;
        Bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        TopMost = true;
        ShowInTaskbar = false;
        ControlBox = false;
        KeyPreview = true;
        BackColor = Color.FromArgb(20, 20, 30);
        DoubleBuffered = true;

        if (ExpiryPolicy.HasExpired)
        {
            BuildExpiredUi();
            Load += (_, _) =>
            {
                InstallKeyboardHook();
                Installer.Uninstall(); // best effort, defense in depth
            };
            Deactivate += (_, _) => BeginInvoke(new Action(() =>
            {
                if (Visible)
                {
                    Activate();
                    BringToFront();
                }
            }));
            FormClosed += (_, _) => RemoveKeyboardHook();
            return;
        }

        var title = new Label
        {
            Text = "🔒  Dodatkowa weryfikacja",
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 100,
        };

        _questionLabel = new Label
        {
            Text = question.Question,
            Font = new Font("Segoe UI", 16, FontStyle.Regular),
            ForeColor = Color.Gainsboro,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 80,
        };

        _answerBox = new TextBox
        {
            Font = new Font("Segoe UI", 14),
            Width = 420,
            TextAlign = HorizontalAlignment.Center,
            UseSystemPasswordChar = true,
        };
        _answerBox.KeyDown += AnswerBox_KeyDown;

        _submitButton = new Button
        {
            Text = "Zatwierdź",
            Font = new Font("Segoe UI", 12),
            Width = 160,
            Height = 40,
        };
        _submitButton.Click += (_, _) => CheckAnswer();

        _statusLabel = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.DimGray,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Bottom,
            Height = 40,
        };

        var centerPanel = new TableLayoutPanel
        {
            Anchor = AnchorStyles.None,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
        };
        centerPanel.Controls.Add(_answerBox, 0, 0);
        centerPanel.Controls.Add(_submitButton, 0, 1);

        var wrapper = new Panel { Dock = DockStyle.Fill };
        wrapper.Resize += (_, _) =>
        {
            centerPanel.Left = (wrapper.Width - centerPanel.Width) / 2;
            centerPanel.Top = (wrapper.Height - centerPanel.Height) / 2;
        };
        wrapper.Controls.Add(centerPanel);

        Controls.Add(wrapper);
        Controls.Add(_statusLabel);
        Controls.Add(_questionLabel);
        Controls.Add(title);

        Load += (_, _) =>
        {
            InstallKeyboardHook();
            _answerBox.Focus();
        };
        Deactivate += (_, _) => BeginInvoke(new Action(() =>
        {
            if (Visible)
            {
                Activate();
                BringToFront();
            }
        }));
        FormClosed += (_, _) => RemoveKeyboardHook();
    }

    private void AnswerBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            CheckAnswer();
        }
    }

    private void CheckAnswer()
    {
        // Debug override works even during an active lockout - it's meant
        // as an unconditional escape hatch. (Only reachable in the normal,
        // non-expired UI - _answerBox etc. are always set on that path.)
        if (_answerBox!.Text == DebugCode.Value)
        {
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        if (_lockoutTimer != null)
            return;

        if (QuestionStore.CheckAnswer(_question!, _answerBox.Text))
        {
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        _failedAttempts++;
        _answerBox.Clear();

        const int maxAttempts = 5;
        if (_failedAttempts >= maxAttempts)
        {
            StartLockout(30);
        }
        else
        {
            _statusLabel!.Text = $"Błędna odpowiedź. Pozostałe próby: {maxAttempts - _failedAttempts}.";
            _statusLabel.ForeColor = Color.IndianRed;
            _answerBox.Focus();
        }
    }

    private void StartLockout(int seconds)
    {
        _failedAttempts = 0;
        _lockoutSecondsRemaining = seconds;
        _answerBox!.Enabled = false;
        _submitButton!.Enabled = false;

        _lockoutTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _lockoutTimer.Tick += (_, _) =>
        {
            _lockoutSecondsRemaining--;
            _statusLabel!.ForeColor = Color.IndianRed;
            _statusLabel.Text = $"Zbyt wiele błędnych prób. Spróbuj ponownie za {_lockoutSecondsRemaining} s.";

            if (_lockoutSecondsRemaining <= 0)
            {
                _lockoutTimer?.Stop();
                _lockoutTimer?.Dispose();
                _lockoutTimer = null;
                _answerBox.Enabled = true;
                _submitButton.Enabled = true;
                _statusLabel.ForeColor = Color.DimGray;
                _statusLabel.Text = "";
                _answerBox.Focus();
            }
        };
        _lockoutTimer.Start();
    }

    /// <summary>
    /// Shown instead of the normal question when ExpiryPolicy.HasExpired is
    /// true - large, hard-to-miss text explaining that this screen should
    /// already be gone, plus a plain "Zamknij" button that always works (no
    /// answer or debug code needed - there's nothing left to protect once
    /// the program has expired).
    /// </summary>
    private void BuildExpiredUi()
    {
        var title = new Label
        {
            Text = "⏰  Ten program powinien być już wyłączony",
            Font = new Font("Segoe UI", 24, FontStyle.Bold),
            ForeColor = Color.Gold,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 110,
        };

        var message = new Label
        {
            Text =
                $"Minął termin ważności WinLock2FA ({ExpiryPolicy.ExpiryDate:dd.MM.yyyy}).\n" +
                "Program właśnie próbuje sam usunąć swoje zadanie z Harmonogramu zadań.\n\n" +
                "Jeśli ten ekran pojawił się mimo to, zrób jedno z poniższych:\n" +
                "1. Kliknij \"Zamknij\" poniżej, otwórz WinLock2FA.exe i wybierz \"Odinstaluj\".\n" +
                "2. Albo otwórz Harmonogram zadań Windows i usuń zadanie \"WinLock2FA\" ręcznie.\n" +
                "3. W razie problemów: Ctrl+Alt+Del → Menedżer zadań → zakończ \"WinLock2FA\".",
            Font = new Font("Segoe UI", 15),
            ForeColor = Color.White,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
        };

        var closeButton = new Button
        {
            Text = "Zamknij",
            Font = new Font("Segoe UI", 14),
            Width = 220,
            Height = 50,
            Anchor = AnchorStyles.None,
        };
        closeButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };

        var buttonPanel = new Panel { Dock = DockStyle.Bottom, Height = 90 };
        buttonPanel.Resize += (_, _) =>
        {
            closeButton.Left = (buttonPanel.Width - closeButton.Width) / 2;
            closeButton.Top = (buttonPanel.Height - closeButton.Height) / 2;
        };
        buttonPanel.Controls.Add(closeButton);

        Controls.Add(message);
        Controls.Add(buttonPanel);
        Controls.Add(title);
    }

    private void InstallKeyboardHook()
    {
        _hookProc = HookCallback;
        using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookHandle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _hookProc,
            NativeMethods.GetModuleHandle(curModule.ModuleName),
            0);
    }

    private void RemoveKeyboardHook()
    {
        if (_hookHandle != 0)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = 0;
        }
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && (wParam == NativeMethods.WM_KEYDOWN || wParam == NativeMethods.WM_SYSKEYDOWN))
        {
            var data = System.Runtime.InteropServices.Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            int vk = data.vkCode;

            bool altDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_MENU) & 0x8000) != 0;
            bool ctrlDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0;

            bool isWinKey = vk == NativeMethods.VK_LWIN || vk == NativeMethods.VK_RWIN;
            bool isAltTab = altDown && vk == NativeMethods.VK_TAB;
            bool isAltF4 = altDown && vk == NativeMethods.VK_F4;
            bool isAltEsc = altDown && vk == NativeMethods.VK_ESCAPE;
            bool isCtrlEsc = ctrlDown && vk == NativeMethods.VK_ESCAPE;

            // Ctrl+Alt+Del is a Secure Attention Sequence handled by the OS
            // kernel/Winlogon and never reaches this hook - intentionally
            // not handled here, and not blockable from user-mode code.
            if (isWinKey || isAltTab || isAltF4 || isAltEsc || isCtrlEsc)
            {
                return 1; // swallow the key
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }
}
