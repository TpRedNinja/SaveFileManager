using System.Windows;
using SavefileManager.Services;

namespace SavefileManager.Views;

public partial class InputDialog : Window
{
    public string? Result { get; private set; }

    public InputDialog(string title, string prompt, string initial = "")
    {
        InitializeComponent();
        ThemeManager.Attach(this);
        Title = title;
        PromptText.Text = prompt;
        InputBox.Text = initial;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        InputBox.Focus();
        InputBox.SelectAll();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Result = InputBox.Text;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
