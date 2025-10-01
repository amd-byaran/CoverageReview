using System;
using System.Net.Http;
using System.Text;
using System.Windows;

namespace CoverageAnalyzerGUI
{
    public partial class HttpAuthDialog : Window
    {
        public HttpClient? AuthenticatedHttpClient { get; private set; }
        public bool RememberCredentials { get; private set; }
        public string Username { get; private set; } = string.Empty;
        public string Password { get; private set; } = string.Empty;

        public HttpAuthDialog(string serverUrl, string? defaultUsername = null)
        {
            InitializeComponent();
            
            ServerLabel.Text = serverUrl;
            
            if (!string.IsNullOrEmpty(defaultUsername))
            {
                UsernameTextBox.Text = defaultUsername;
                PasswordBox.Focus();
            }
            else
            {
                UsernameTextBox.Focus();
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var username = UsernameTextBox.Text?.Trim();
            var password = PasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowStatus("Please enter both username and password.", isError: true);
                return;
            }

            try
            {
                // Create HttpClient with basic authentication
                var httpClient = new HttpClient();
                var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
                httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

                // Set a reasonable timeout
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                AuthenticatedHttpClient = httpClient;
                RememberCredentials = RememberCheckBox.IsChecked == true;
                Username = username;
                Password = password;

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowStatus($"Error creating authentication: {ex.Message}", isError: true);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ShowStatus(string message, bool isError = false)
        {
            StatusMessage.Text = message;
            StatusMessage.Foreground = isError ? 
                System.Windows.Media.Brushes.Red : 
                System.Windows.Media.Brushes.Green;
            StatusMessage.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Static method to show the HTTP authentication dialog
        /// </summary>
        /// <param name="parent">Parent window</param>
        /// <param name="serverUrl">Server URL for display</param>
        /// <param name="defaultUsername">Default username to pre-fill</param>
        /// <returns>Tuple of (success, httpClient, rememberCredentials, username, password)</returns>
        public static (bool success, HttpClient? httpClient, bool rememberCredentials, string username, string password) GetHttpAuthentication(
            Window parent, string serverUrl, string? defaultUsername = null)
        {
            var dialog = new HttpAuthDialog(serverUrl, defaultUsername);
            dialog.Owner = parent;
            
            var result = dialog.ShowDialog();
            
            if (result == true)
            {
                return (true, dialog.AuthenticatedHttpClient, dialog.RememberCredentials, dialog.Username, dialog.Password);
            }
            else
            {
                // Clean up if dialog was cancelled
                dialog.AuthenticatedHttpClient?.Dispose();
                return (false, null, false, string.Empty, string.Empty);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // If dialog result is not true, clean up the HttpClient
            if (DialogResult != true)
            {
                AuthenticatedHttpClient?.Dispose();
                AuthenticatedHttpClient = null;
            }
            
            base.OnClosed(e);
        }
    }
}