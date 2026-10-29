using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SavefileManager.Services;

namespace SavefileManager.Views;

public partial class MessageDialog : Window
{
    public bool Accepted { get; private set; }

    public MessageDialog()
    {
        InitializeComponent();   // only used by the designer / XAML loader
    }

    public MessageDialog(string title, string message, string okText, string? cancelText) : this()
    {
        ThemeManager.Attach(this);
        Title = title;
        MessageText.Text = message;
        OkButton.Content = okText;
        if (cancelText == null)
            CancelButton.IsVisible = false;
        else
            CancelButton.Content = cancelText;
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { Accepted = true; Close(); e.Handled = true; }
        else if (e.Key == Key.Escape) { Accepted = false; Close(); e.Handled = true; }
    }

    private void Ok_Click(object? sender, RoutedEventArgs e) { Accepted = true; Close(); }
    private void Cancel_Click(object? sender, RoutedEventArgs e) { Accepted = false; Close(); }
}
