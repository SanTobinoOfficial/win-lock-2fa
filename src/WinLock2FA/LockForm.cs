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
/// </summary>
public class LockForm : Form
{
    /// <summary>
    /// Hardcoded emergency/debug override: typing this into the answer box
    /// force-closes the lock regardless of the actual question or an active
    /// lockout. This is NOT a secret - it's committed in a public repo, so
    /// it gives zero protection against anyone who can read the source.
    /// It exists purely as a convenience escape hatch alongside Ctrl+Alt+Del
    /// for the person building/testing this on their own machine. Change or
    /// remove it before relying on this app for anything more than that.
    /// </summary>
    private const string DebugOverrideCode = "0000";

    private readonly QuestionEntry _question;
    private readonly Label _questionLabel;
    private readonly TextBox _answerBox;
    private readonly Label _statusLabel;
    private readonly Button _submitButton;

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
            Text = "Naciśnij Ctrl+Alt+Del i otwórz Menedżera zadań, jeśli musisz to przerwać.",
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
        // as an unconditional escape hatch.
        if (_answerBox.Text == DebugOverrideCode)
        {
            Close();
            return;
        }

        if (_lockoutTimer != null)
            return;

        if (QuestionStore.CheckAnswer(_question, _answerBox.Text))
        {
            // Close() (not Application.Exit()) so a nested test run from the
            // menu returns control to the caller instead of killing the
            // whole process. Application.Run(this) below exits on Close().
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
            _statusLabel.Text = $"Błędna odpowiedź. Pozostałe próby: {maxAttempts - _failedAttempts}.";
            _statusLabel.ForeColor = Color.IndianRed;
            _answerBox.Focus();
        }
    }

    private void StartLockout(int seconds)
    {
        _failedAttempts = 0;
        _lockoutSecondsRemaining = seconds;
        _answerBox.Enabled = false;
        _submitButton.Enabled = false;

        _lockoutTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _lockoutTimer.Tick += (_, _) =>
        {
            _lockoutSecondsRemaining--;
            _statusLabel.ForeColor = Color.IndianRed;
            _statusLabel.Text = $"Zbyt wiele błędnych prób. Spróbuj ponownie za {_lockoutSecondsRemaining} s.";

            if (_lockoutSecondsRemaining <= 0)
            {
                _lockoutTimer?.Stop();
                _lockoutTimer?.Dispose();
                _lockoutTimer = null;
                _answerBox.Enabled = true;
                _submitButton.Enabled = true;
                _statusLabel.ForeColor = Color.DimGray;
                _statusLabel.Text = "Naciśnij Ctrl+Alt+Del i otwórz Menedżera zadań, jeśli musisz to przerwać.";
                _answerBox.Focus();
            }
        };
        _lockoutTimer.Start();
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
