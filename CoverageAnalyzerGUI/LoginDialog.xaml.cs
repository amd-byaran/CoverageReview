using System;
using System.Windows;

namespace CoverageAnalyzerGUI
{
    /// <summary>
    /// Interaction logic for LoginDialog.xaml
    /// </summary>
    public partial class LoginDialog : Window
    {
        public string Username { get; private set; } = string.Empty;
        public string Password { get; private set; } = string.Empty;
        public bool IsAuthenticated { get; private set; } = false;

        public LoginDialog(string serverUrl)
        {
            InitializeComponent();
            ServerTextBlock.Text = $"Server: {serverUrl}";
            UsernameTextBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                MessageBox.Show("Please enter a username.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                UsernameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                MessageBox.Show("Please enter a password.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                PasswordBox.Focus();
                return;
            }

            Username = UsernameTextBox.Text.Trim();
            Password = PasswordBox.Password;
            IsAuthenticated = true;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsAuthenticated = false;
            DialogResult = false;
            Close();
        }
    }
}