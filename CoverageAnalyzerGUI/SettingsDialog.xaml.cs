using System;
using System.Windows;

namespace CoverageAnalyzerGUI
{
    /// <summary>
    /// Settings dialog for configuring application-wide settings like Jira Project
    /// </summary>
    public partial class SettingsDialog : Window
    {
        /// <summary>
        /// Gets the Jira Server URL entered by the user
        /// </summary>
        public string JiraServer => JiraServerTextBox.Text?.Trim() ?? string.Empty;
        
        /// <summary>
        /// Gets the Jira Project name entered by the user
        /// </summary>
        public string JiraProject => JiraProjectTextBox.Text?.Trim() ?? string.Empty;

        /// <summary>
        /// Initializes a new instance of the SettingsDialog
        /// </summary>
        /// <param name="currentJiraServer">Current Jira server URL to pre-populate the field</param>
        /// <param name="currentJiraProject">Current Jira project name to pre-populate the field</param>
        public SettingsDialog(string currentJiraServer = "", string currentJiraProject = "")
        {
            InitializeComponent();
            
            // Pre-populate with current values
            JiraServerTextBox.Text = currentJiraServer ?? string.Empty;
            JiraProjectTextBox.Text = currentJiraProject ?? string.Empty;
            
            // Focus on the server text box
            JiraServerTextBox.Focus();
            JiraServerTextBox.SelectAll();
        }

        /// <summary>
        /// Handles OK button click - validates input and closes dialog
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate input
                var jiraServer = JiraServer;
                var jiraProject = JiraProject;
                
                if (string.IsNullOrWhiteSpace(jiraServer))
                {
                    MessageBox.Show(
                        "Please enter a Jira Server URL.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    JiraServerTextBox.Focus();
                    return;
                }
                
                if (string.IsNullOrWhiteSpace(jiraProject))
                {
                    MessageBox.Show(
                        "Please enter a Jira Project name.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    JiraProjectTextBox.Focus();
                    return;
                }

                // Validate URL format
                if (!Uri.TryCreate(jiraServer, UriKind.Absolute, out var uri) || 
                    (uri.Scheme != "http" && uri.Scheme != "https"))
                {
                    MessageBox.Show(
                        "Please enter a valid HTTP or HTTPS URL for Jira Server.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    JiraServerTextBox.Focus();
                    return;
                }

                // Check for invalid characters in project name (basic validation)
                if (jiraProject.Contains("/") || jiraProject.Contains("\\") || jiraProject.Contains(":"))
                {
                    MessageBox.Show(
                        "Jira Project name cannot contain special characters like /, \\, or :",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    JiraProjectTextBox.Focus();
                    return;
                }

                // Set dialog result and close
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error validating settings: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handles Cancel button click - closes dialog without saving
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}