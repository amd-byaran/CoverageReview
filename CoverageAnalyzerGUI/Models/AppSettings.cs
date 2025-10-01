using Newtonsoft.Json;
using System;
using System.IO;

namespace CoverageAnalyzerGUI.Models
{
    /// <summary>
    /// Application-wide settings stored in user's AppData folder
    /// </summary>
    public class AppSettings
    {
    [JsonProperty("jiraServer")]
    public string JiraServer { get; set; } = "https://ontrack-internal.amd.com/";

    [JsonProperty("jiraProject")]
    public string JiraProject { get; set; } = "DCNDVDBG";

    [JsonProperty("lastModified")]
    public DateTime LastModified { get; set; } = DateTime.Now;        /// <summary>
        /// Gets the path to the application settings file
        /// </summary>
        private static string SettingsFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CoverageAnalyzer",
            "appsettings.json");

        /// <summary>
        /// Saves the application settings to the settings file
        /// </summary>
        public void Save()
        {
            try
            {
                LastModified = DateTime.Now;

                // Ensure directory exists
                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving app settings: {ex.Message}");
                // Don't throw - settings are not critical for app functionality
            }
        }

        /// <summary>
        /// Loads application settings from the settings file
        /// </summary>
        /// <returns>AppSettings instance, or default settings if file doesn't exist or can't be loaded</returns>
        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonConvert.DeserializeObject<AppSettings>(json);
                    return settings ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading app settings: {ex.Message}");
                // Fall through to return default settings
            }

            return new AppSettings();
        }

        /// <summary>
        /// Gets the current Jira Project setting
        /// </summary>
        /// <returns>Jira Project name with default value if not set</returns>
        public static string GetJiraProject()
        {
            var settings = Load();
            return settings.JiraProject;
        }
        
        /// <summary>
        /// Gets the current Jira Server setting
        /// </summary>
        /// <returns>Jira Server URL with default value if not set</returns>
        public static string GetJiraServer()
        {
            var settings = Load();
            return settings.JiraServer;
        }
    }
}