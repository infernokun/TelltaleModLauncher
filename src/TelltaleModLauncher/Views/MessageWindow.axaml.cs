using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TelltaleModLauncher.Views;

/// <summary>A message or a yes/no question. Closes with true when the confirm button is used.</summary>
public partial class MessageWindow : Window
{
    public MessageWindow()
    {
        InitializeComponent();
    }

    public MessageWindow(string title, string message, string confirmText, string? cancelText, bool destructive) : this()
    {
        Title = title;
        Heading.Text = title.ToUpperInvariant();
        Body.Text = message;
        ConfirmButton.Content = confirmText;
        ConfirmButton.Classes.Add(destructive ? "danger" : "primary");
        CancelButton.Content = cancelText;
        CancelButton.IsVisible = cancelText != null;
    }

    private void OnConfirm(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
