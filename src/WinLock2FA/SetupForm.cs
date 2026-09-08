using System.Drawing;
using System.Windows.Forms;

namespace WinLock2FA;

/// <summary>
/// Ordinary (non-blocking) window used to add, edit and remove security
/// question/answer pairs. Answers are never displayed or stored in plain
/// text - see QuestionStore.
/// </summary>
public class SetupForm : Form
{
    private readonly ListBox _list;
    private List<QuestionEntry> _entries;

    public SetupForm()
    {
        Text = "WinLock2FA — Pytania bezpieczeństwa";
        Width = 560;
        Height = 420;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;

        _entries = QuestionStore.Load();

        _list = new ListBox
        {
            Left = 20,
            Top = 20,
            Width = 500,
            Height = 260,
            Font = new Font("Segoe UI", 10),
        };
        RefreshList();

        var addButton = new Button { Text = "Dodaj...", Left = 20, Top = 295, Width = 110 };
        addButton.Click += (_, _) => AddQuestion();

        var removeButton = new Button { Text = "Usuń zaznaczone", Left = 140, Top = 295, Width = 140 };
        removeButton.Click += (_, _) => RemoveSelected();

        var infoLabel = new Label
        {
            Text = "Zalecane min. 3 pytania. Odpowiedzi są hashowane (SHA-256 + sól)\ni szyfrowane DPAPI — nikt nie zobaczy ich w pliku.",
            Left = 20,
            Top = 330,
            Width = 500,
            Height = 40,
            ForeColor = Color.DimGray,
        };

        var saveButton = new Button { Text = "Zapisz i zamknij", Left = 380, Top = 295, Width = 140 };
        saveButton.Click += (_, _) =>
        {
            QuestionStore.Save(_entries);
            Close();
        };

        Controls.Add(_list);
        Controls.Add(addButton);
        Controls.Add(removeButton);
        Controls.Add(saveButton);
        Controls.Add(infoLabel);
    }

    private void RefreshList()
    {
        _list.Items.Clear();
        foreach (var e in _entries)
            _list.Items.Add(e.Question);
    }

    private void AddQuestion()
    {
        using var dialog = new QuestionDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _entries.Add(QuestionStore.CreateEntry(dialog.Question, dialog.Answer));
            RefreshList();
        }
    }

    private void RemoveSelected()
    {
        if (_list.SelectedIndex < 0)
            return;

        _entries.RemoveAt(_list.SelectedIndex);
        RefreshList();
    }
}

/// <summary>Small modal dialog for entering one question/answer pair.</summary>
public class QuestionDialog : Form
{
    private readonly TextBox _questionBox;
    private readonly TextBox _answerBox;

    public string Question => _questionBox.Text.Trim();
    public string Answer => _answerBox.Text;

    public QuestionDialog()
    {
        Text = "Nowe pytanie";
        Width = 420;
        Height = 220;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;

        var questionLabel = new Label { Text = "Pytanie:", Left = 20, Top = 20, Width = 360 };
        _questionBox = new TextBox { Left = 20, Top = 45, Width = 360 };

        var answerLabel = new Label { Text = "Odpowiedź (bez rozróżniania wielkości liter/spacji):", Left = 20, Top = 80, Width = 360 };
        _answerBox = new TextBox { Left = 20, Top = 105, Width = 360 };

        var okButton = new Button { Text = "OK", Left = 220, Top = 140, Width = 80, DialogResult = DialogResult.OK };
        okButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_questionBox.Text) || string.IsNullOrWhiteSpace(_answerBox.Text))
            {
                MessageBox.Show(this, "Uzupełnij pytanie i odpowiedź.", "Brak danych", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        };

        var cancelButton = new Button { Text = "Anuluj", Left = 300, Top = 140, Width = 80, DialogResult = DialogResult.Cancel };

        Controls.Add(questionLabel);
        Controls.Add(_questionBox);
        Controls.Add(answerLabel);
        Controls.Add(_answerBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }
}
