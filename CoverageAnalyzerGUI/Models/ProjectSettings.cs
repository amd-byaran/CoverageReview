using Newtonsoft.Json;
using System;
using System.IO;

namespace CoverageAnalyzerGUI.Models;

/// <summary>
/// Represents the settings and configuration for a coverage analysis project
/// </summary>
public class ProjectSettings
{
    [JsonProperty("projectName")]
    public string ProjectName { get; set; } = string.Empty;

    [JsonProperty("projectFolderPath")]
    public string ProjectFolderPath { get; set; } = string.Empty;

    [JsonProperty("selectedRelease")]
    public DatabaseRelease? SelectedRelease { get; set; }

    [JsonProperty("selectedReport")]
    public DatabaseReport? SelectedReport { get; set; }

    [JsonProperty("coverageType")]
    public CoverageType CoverageType { get; set; } = CoverageType.Functional;

    [JsonProperty("reportType")]
    public ReportType ReportType { get; set; } = ReportType.Individual;

    [JsonProperty("selectedChangelist")]
    public string SelectedChangelist { get; set; } = string.Empty;

    [JsonProperty("reportPath")]
    public string ReportPath { get; set; } = string.Empty;

    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonProperty("lastModified")]
    public DateTime LastModified { get; set; } = DateTime.Now;

    [JsonProperty("httpServerUrl")]
    public string HttpServerUrl { get; set; } = string.Empty;

    [JsonProperty("hvpTop")]
    public string HvpTop { get; set; } = string.Empty;

    [JsonProperty("projectTestDef")]
    public string ProjectTestDef { get; set; } = string.Empty;

    [JsonProperty("reportNameWithoutVerifPlan")]
    public string ReportNameWithoutVerifPlan { get; set; } = string.Empty;

    [JsonProperty("jiraEpic")]
    public string JiraEpic { get; set; } = string.Empty;

    [JsonProperty("jiraStory")]
    public string JiraStory { get; set; } = string.Empty;

    [JsonProperty("jiraServer")]
    public string JiraServer { get; set; } = string.Empty;

    [JsonProperty("jiraProject")]
    public string JiraProject { get; set; } = string.Empty;

    /// <summary>
    /// Full local data path for internal use
    /// </summary>
    [JsonIgnore]
    public string LocalDataPath => Path.Combine(ProjectFolderPath, "data");

    /// <summary>
    /// Relative local data path for JSON serialization
    /// </summary>
    [JsonProperty("localDataPath")]
    public string JsonLocalDataPath => Path.Combine(ProjectName, "data");

    /// <summary>
    /// Gets the full path to the project settings file
    /// </summary>
    public string SettingsFilePath => Path.Combine(ProjectFolderPath, "project.json");

    /// <summary>
    /// Saves the project settings to a JSON file
    /// </summary>
    public void Save()
    {
        LastModified = DateTime.Now;
        
        // Ensure project directory exists
        if (!Directory.Exists(ProjectFolderPath))
        {
            Directory.CreateDirectory(ProjectFolderPath);
        }

        // Ensure data directory exists (use full path)
        if (!Directory.Exists(LocalDataPath))
        {
            Directory.CreateDirectory(LocalDataPath);
        }

        var json = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(SettingsFilePath, json);
    }

    /// <summary>
    /// Generate Jira Epic and Story names based on project settings
    /// </summary>
    public void GenerateJiraFields()
    {
        // Generate JiraEpic: {release_name}_{coverage_type}
        // Example: dcn6_0_func, dcn6_0_code
        if (SelectedRelease != null && !string.IsNullOrEmpty(SelectedRelease.Name))
        {
            var releaseName = SelectedRelease.Name.ToLower().Replace(".", "_").Replace("-", "_");
            var coverageTypeStr = CoverageType == CoverageType.Functional ? "func" : "code";
            JiraEpic = $"{releaseName}_{coverageTypeStr}";
        }
        else
        {
            JiraEpic = string.Empty;
        }

        // Generate JiraStory: {release_name}_{report_name}
        // Example: dcn6_0_dc_core_verif_plan, dcn6_0_gpu_shader_verif_plan
        if (SelectedRelease != null && !string.IsNullOrEmpty(SelectedRelease.Name) && 
            SelectedReport != null && !string.IsNullOrEmpty(SelectedReport.Name))
        {
            var releaseName = SelectedRelease.Name.ToLower().Replace(".", "_").Replace("-", "_");
            var reportName = SelectedReport.Name.ToLower().Replace(".", "_").Replace("-", "_");
            JiraStory = $"{releaseName}_{reportName}";
        }
        else
        {
            JiraStory = string.Empty;
        }
    }

    /// <summary>
    /// Loads project settings from a JSON file
    /// </summary>
    public static ProjectSettings? Load(string projectFolderPath)
    {
        var settingsPath = Path.Combine(projectFolderPath, "project.json");
        
        if (!File.Exists(settingsPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(settingsPath);
            System.Diagnostics.Debug.WriteLine($"[ProjectSettings.Load] JSON content: {json}");
            
            var settings = JsonConvert.DeserializeObject<ProjectSettings>(json);
            System.Diagnostics.Debug.WriteLine($"[ProjectSettings.Load] Deserialized - JiraServer: '{settings?.JiraServer}', JiraProject: '{settings?.JiraProject}'");
            
            // Ensure the project folder path is updated in case the project was moved
            if (settings != null)
            {
                settings.ProjectFolderPath = projectFolderPath;
                
                // Backward compatibility: populate ReportNameWithoutVerifPlan if not set
                if (string.IsNullOrEmpty(settings.ReportNameWithoutVerifPlan) && 
                    settings.SelectedReport != null && 
                    !string.IsNullOrEmpty(settings.SelectedReport.Name))
                {
                    var reportName = settings.SelectedReport.Name;
                    settings.ReportNameWithoutVerifPlan = reportName.EndsWith("_verif_plan") 
                        ? reportName.Substring(0, reportName.Length - "_verif_plan".Length) 
                        : reportName;
                    
                    // Save the updated settings
                    settings.Save();
                }
            }
            
            return settings;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Validates that all required settings are configured
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(ProjectName) &&
               !string.IsNullOrEmpty(ProjectFolderPath) &&
               SelectedRelease != null &&
               SelectedReport != null &&
               !string.IsNullOrEmpty(SelectedChangelist) &&
               !string.IsNullOrEmpty(ReportPath);
    }

    /// <summary>
    /// Converts CoverageType enum to internal database string
    /// </summary>
    public string GetCoverageTypeString()
    {
        return CoverageType switch
        {
            CoverageType.Functional => "func_cov",
            CoverageType.Code => "code_cov",
            _ => "func_cov"
        };
    }

    /// <summary>
    /// Converts ReportType enum to internal database string
    /// </summary>
    public string GetReportTypeString()
    {
        return ReportType switch
        {
            ReportType.Individual => "individual",
            ReportType.Accumulate => "accumulate",
            _ => "individual"
        };
    }
}

/// <summary>
/// Represents a database project
/// </summary>
public class DatabaseProject
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    public override string ToString() => Name;
}

/// <summary>
/// Represents a database release
/// </summary>
public class DatabaseRelease
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("projectId")]
    public int ProjectId { get; set; }

    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; }

    public override string ToString() => Name;
}

/// <summary>
/// Represents a database report
/// </summary>
public class DatabaseReport
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("releaseId")]
    public int ReleaseId { get; set; }

    [JsonProperty("projectName")]
    public string ProjectName { get; set; } = string.Empty;

    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; }

    public override string ToString() => Name;
}

/// <summary>
/// Types of coverage analysis
/// </summary>
public enum CoverageType
{
    [JsonProperty("functional")]
    Functional,
    
    [JsonProperty("code")]
    Code
}

/// <summary>
/// Types of reports
/// </summary>
public enum ReportType
{
    [JsonProperty("individual")]
    Individual,
    
    [JsonProperty("accumulate")]
    Accumulate
}