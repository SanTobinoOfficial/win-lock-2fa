using System.Windows.Forms;

namespace WinLock2FA;

/// <summary>Small modal dialog asking for the debug/emergency code, masked like a password field.</summary>
public class CodePromptDialog : Form
{
    private readonly TextBox _codeBox;

    public string EnteredCode => _codeBox.Text;

    public CodePromptDialog(string message)
    {
        Text = "Wymagany kod";
        Width = 380;
        Height = 170;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;

        var label = new Label { Text = message, Left = 20, Top = 15, Width = 330, Height = 40 };

        _codeBox = new TextBox
        {
            Left = 20,
            Top = 60,
            Width = 330,
            UseSystemPasswordChar = true,
        };

        var okButton = new Button { Text = "OK", Left = 170, Top = 95, Width = 80, DialogResult = DialogResult.OK };
        var cancelButton = new Button { Text = "Anuluj", Left = 260, Top = 95, Width = 90, DialogResult = DialogResult.Cancel };

        Controls.Add(label);
        Controls.Add(_codeBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }
}
