using System.Windows;
using SavefileManager.Services;

namespace SavefileManager.Views;

public partial class MessageDialog : Window
{
    public MessageDialog(string title, string message, string okText, string? cancelText)
    {
        InitializeComponent();
        ThemeManager.Attach(this);
        Title = title;
        MessageText.Text = message;
        OkButton.Content = okText;
        if (cancelText == null)
            CancelButton.Visibility = Visibility.Collapsed;
        else
            CancelButton.Content = cancelText;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
