using System.Windows;

namespace CoverageAnalyzerGUI
{
    public partial class SshCredentialsDialog : Window
    {
        public string Username { get; private set; } = "";
        public string Password { get; private set; } = "";
        public bool WasAccepted { get; private set; } = false;

        public SshCredentialsDialog(string hostname, string? defaultUsername = null)
        {
            InitializeComponent();
            
            // Set the hostname in the header
            HostLabel.Text = $"Connecting to: {hostname}";
            
            // Set default username if provided
            if (!string.IsNullOrEmpty(defaultUsername))
            {
                UsernameTextBox.Text = defaultUsername;
                PasswordBox.Focus(); // Focus password if username is pre-filled
            }
            else
            {
                UsernameTextBox.Focus(); // Focus username if empty
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                MessageBox.Show("Please enter a username.", "Validation Error", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                UsernameTextBox.Focus();
                return;
            }

            if (string.IsNullOrEmpty(PasswordBox.Password))
            {
                MessageBox.Show("Please enter a password.", "Validation Error", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                PasswordBox.Focus();
                return;
            }

            // Set results
            Username = UsernameTextBox.Text.Trim();
            Password = PasswordBox.Password;
            WasAccepted = true;
            
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            WasAccepted = false;
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Static helper method to show the dialog and get credentials
        /// </summary>
        /// <param name="owner">Parent window</param>
        /// <param name="hostname">SSH hostname for display</param>
        /// <param name="defaultUsername">Default username to pre-fill</param>
        /// <returns>Tuple with success flag, username, and password</returns>
        public static (bool success, string username, string password) GetCredentials(
            Window? owner, string hostname, string? defaultUsername = null)
        {
            var dialog = new SshCredentialsDialog(hostname, defaultUsername);
            if (owner != null)
            {
                dialog.Owner = owner;
            }

            dialog.ShowDialog();

            return (dialog.WasAccepted, dialog.Username, dialog.Password);
        }
    }
}