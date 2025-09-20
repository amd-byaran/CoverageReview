using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CoverageAnalyzerGUI.Utilities
{
    public class SshHostConfig
    {
        public string HostPattern { get; set; } = "";
        public string? HostName { get; set; }
        public string? User { get; set; }
        public int Port { get; set; } = 22;
        public List<string> IdentityFiles { get; set; } = new();
        public string? PreferredAuthentications { get; set; }
        public bool PasswordAuthentication { get; set; } = true;
        public bool PubkeyAuthentication { get; set; } = true;
    }

    public static class SshConfigParser
    {
        /// <summary>
        /// Parse SSH config file and return configuration for a specific host
        /// </summary>
        /// <param name="hostname">The hostname to look up</param>
        /// <param name="configPath">Optional path to SSH config file</param>
        /// <returns>SSH configuration for the host, or null if not found</returns>
        public static SshHostConfig? GetHostConfig(string hostname, string? configPath = null)
        {
            if (string.IsNullOrEmpty(hostname))
                return null;

            configPath ??= GetDefaultConfigPath();
            
            if (!File.Exists(configPath))
            {
                Console.WriteLine($"SSH config file not found: {configPath}");
                return CreateDefaultConfig(hostname);
            }

            try
            {
                var hostConfigs = ParseConfigFile(configPath);
                
                // Find the first matching host configuration
                var matchingConfig = hostConfigs.FirstOrDefault(config => 
                    HostMatches(config.HostPattern, hostname));

                if (matchingConfig != null)
                {
                    Console.WriteLine($"Found SSH config for host '{hostname}': User={matchingConfig.User}, IdentityFiles={string.Join(", ", matchingConfig.IdentityFiles)}");
                    return matchingConfig;
                }
                else
                {
                    Console.WriteLine($"No specific SSH config found for host '{hostname}', using defaults");
                    return CreateDefaultConfig(hostname);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing SSH config: {ex.Message}");
                return CreateDefaultConfig(hostname);
            }
        }

        /// <summary>
        /// Get the default SSH config file path
        /// </summary>
        public static string GetDefaultConfigPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh", "config");
        }

        /// <summary>
        /// Parse the entire SSH config file and return all host configurations
        /// </summary>
        private static List<SshHostConfig> ParseConfigFile(string configPath)
        {
            var hostConfigs = new List<SshHostConfig>();
            var lines = File.ReadAllLines(configPath);
            
            SshHostConfig? currentConfig = null;
            var globalConfig = new SshHostConfig { HostPattern = "*" };

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                
                // Skip empty lines and comments
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;

                // Parse key-value pairs
                var parts = line.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    continue;

                var key = parts[0].ToLowerInvariant();
                var value = parts[1].Trim();

                switch (key)
                {
                    case "host":
                        // Save previous host config
                        if (currentConfig != null)
                        {
                            hostConfigs.Add(currentConfig);
                        }
                        
                        // Start new host config
                        currentConfig = new SshHostConfig 
                        { 
                            HostPattern = value,
                            // Inherit from global config
                            Port = globalConfig.Port,
                            PasswordAuthentication = globalConfig.PasswordAuthentication,
                            PubkeyAuthentication = globalConfig.PubkeyAuthentication
                        };
                        currentConfig.IdentityFiles.AddRange(globalConfig.IdentityFiles);
                        break;

                    case "hostname":
                        if (currentConfig != null)
                            currentConfig.HostName = value;
                        break;

                    case "user":
                        if (currentConfig != null)
                            currentConfig.User = value;
                        else
                            globalConfig.User = value;
                        break;

                    case "port":
                        if (int.TryParse(value, out int port))
                        {
                            if (currentConfig != null)
                                currentConfig.Port = port;
                            else
                                globalConfig.Port = port;
                        }
                        break;

                    case "identityfile":
                        var identityFile = ExpandPath(value);
                        if (currentConfig != null)
                            currentConfig.IdentityFiles.Add(identityFile);
                        else
                            globalConfig.IdentityFiles.Add(identityFile);
                        break;

                    case "preferredauthentications":
                        if (currentConfig != null)
                            currentConfig.PreferredAuthentications = value;
                        else
                            globalConfig.PreferredAuthentications = value;
                        break;

                    case "passwordauthentication":
                        var passwordAuth = value.ToLowerInvariant() == "yes";
                        if (currentConfig != null)
                            currentConfig.PasswordAuthentication = passwordAuth;
                        else
                            globalConfig.PasswordAuthentication = passwordAuth;
                        break;

                    case "pubkeyauthentication":
                        var pubkeyAuth = value.ToLowerInvariant() == "yes";
                        if (currentConfig != null)
                            currentConfig.PubkeyAuthentication = pubkeyAuth;
                        else
                            globalConfig.PubkeyAuthentication = pubkeyAuth;
                        break;
                }
            }

            // Add the last host config
            if (currentConfig != null)
            {
                hostConfigs.Add(currentConfig);
            }

            return hostConfigs;
        }

        /// <summary>
        /// Check if a host pattern matches a hostname
        /// </summary>
        private static bool HostMatches(string pattern, string hostname)
        {
            if (pattern == "*")
                return true;

            if (pattern == hostname)
                return true;

            // Convert SSH wildcard pattern to regex
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace(@"\*", ".*")
                .Replace(@"\?", ".") + "$";

            return Regex.IsMatch(hostname, regexPattern, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Expand SSH config path variables
        /// </summary>
        private static string ExpandPath(string path)
        {
            if (path.StartsWith("~/"))
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                                  path.Substring(2));
            }

            if (path.StartsWith("%d/"))
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                                  ".ssh", path.Substring(3));
            }

            return Environment.ExpandEnvironmentVariables(path);
        }

        /// <summary>
        /// Create a default configuration when no SSH config is found
        /// </summary>
        private static SshHostConfig CreateDefaultConfig(string hostname)
        {
            var sshDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
            var defaultIdentityFiles = new List<string>();

            // Add common default identity files if they exist
            var commonKeys = new[] { "id_rsa", "id_ecdsa", "id_ed25519" };
            foreach (var keyName in commonKeys)
            {
                var keyPath = Path.Combine(sshDir, keyName);
                if (File.Exists(keyPath))
                {
                    defaultIdentityFiles.Add(keyPath);
                }
            }

            return new SshHostConfig
            {
                HostPattern = hostname,
                HostName = hostname,
                User = Environment.UserName, // Use current Windows username as default
                Port = 22,
                IdentityFiles = defaultIdentityFiles,
                PasswordAuthentication = true,
                PubkeyAuthentication = true
            };
        }

        /// <summary>
        /// Get all available identity files for a host (from config + defaults)
        /// </summary>
        public static List<string> GetIdentityFiles(string hostname)
        {
            var config = GetHostConfig(hostname);
            if (config == null)
                return new List<string>();

            var identityFiles = new List<string>(config.IdentityFiles);

            // If no identity files specified, add defaults
            if (identityFiles.Count == 0)
            {
                var sshDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
                var defaultKeys = new[] { "id_rsa", "id_ecdsa", "id_ed25519" };
                
                foreach (var keyName in defaultKeys)
                {
                    var keyPath = Path.Combine(sshDir, keyName);
                    if (File.Exists(keyPath))
                    {
                        identityFiles.Add(keyPath);
                    }
                }
            }

            // Filter to only existing files
            return identityFiles.Where(File.Exists).ToList();
        }

        /// <summary>
        /// Get the username for a specific host from SSH config
        /// </summary>
        public static string? GetUsername(string hostname)
        {
            var config = GetHostConfig(hostname);
            return config?.User;
        }
    }
}