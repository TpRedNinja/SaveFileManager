using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SavefileManager.Services;

namespace SavefileManager.Views;

public partial class InputDialog : Window
{
    public bool Accepted { get; private set; }
    public string? Result { get; private set; }

    public InputDialog()
    {
        InitializeComponent();
    }

    public InputDialog(string title, string prompt, string initial = "") : this()
    {
        ThemeManager.Attach(this);
        Title = title;
        PromptText.Text = prompt;
        InputBox.Text = initial;
        KeyDown += OnKeyDown;
    }

    private void Window_Opened(object? sender, EventArgs e)
    {
        InputBox.Focus();
        InputBox.SelectAll();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { Accept(); e.Handled = true; }
        else if (e.Key == Key.Escape) { Accepted = false; Close(); e.Handled = true; }
    }

    private void Accept()
    {
        Result = InputBox.Text ?? "";
        Accepted = true;
        Close();
    }

    private void Ok_Click(object? sender, RoutedEventArgs e) => Accept();
    private void Cancel_Click(object? sender, RoutedEventArgs e) { Accepted = false; Close(); }
}
