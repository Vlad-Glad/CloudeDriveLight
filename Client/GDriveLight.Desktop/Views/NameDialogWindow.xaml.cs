using System.Windows;

namespace GDriveLight.Desktop.Views;

public partial class NameDialogWindow : Window
{
    public NameDialogWindow(string title, string prompt, string initialName)
    {
        InitializeComponent();
        Title = title;
        PromptLabel.Text = prompt;
        NameInput.Text = initialName;
        Loaded += (_, _) =>
        {
            NameInput.Focus();
            NameInput.SelectAll();
        };
    }

    public string EnteredName => NameInput.Text.Trim();

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EnteredName))
        {
            ValidationMessage.Text = "A name is required.";
            return;
        }

        DialogResult = true;
    }
}
