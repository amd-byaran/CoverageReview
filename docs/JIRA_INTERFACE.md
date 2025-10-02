# Jira Interface Implementation

## Overview
Added comprehensive Jira interface integration to the Coverage Analyzer with auto-generated Epic and Story fields based on project selections.

## Features Added

### 1. **ProjectSettings Model Extensions**
- **JiraServer**: Jira server URL (e.g., `https://ontrack-internal.amd.com/`)
- **JiraProject**: Jira project key (e.g., `DCNDVDBG`, `RDNA`, `GPU_CORE`)
- **JiraEpic**: `{release_name}_{coverage_type}` (e.g., `dcn6_0_func`, `dcn6_0_code`)
- **JiraStory**: `{release_name}_{report_name}` (e.g., `dcn6_0_dc_core_verif_plan`)
- **Auto-generation method**: `GenerateJiraFields()` creates Epic/Story fields based on current selections
- **JSON serialization**: All Jira fields are stored in project.json files

### 2. **ProjectWizard Background Integration**
- **Invisible to User**: Jira fields are generated automatically in the background
- **No UI Elements**: Keeps wizard clean and focused on core project settings
- **Auto-generation**: Fields update automatically when release, coverage type, or report changes
- **Seamless Integration**: Generated during project creation without user intervention

### 3. **Auto-Generation Logic**

#### JiraEpic Generation:
```csharp
// Format: {release_name}_{coverage_type}
// Examples:
"dcn6.0" + "Functional" → "dcn6_0_func"  
"dcn6.0" + "Code" → "dcn6_0_code"
"rdna4.1" + "Functional" → "rdna4_1_func"
```

#### JiraStory Generation:
```csharp
// Format: {release_name}_{report_name}
// Examples:
"dcn6.0" + "dc_core_verif_plan" → "dcn6_0_dc_core_verif_plan"
"dcn6.0" + "gpu_shader_verif_plan" → "dcn6_0_gpu_shader_verif_plan"
"rdna4.1" + "compute_verif_plan" → "rdna4_1_compute_verif_plan"
```

### 4. **JiraAPI Integration (NuGet Package)**
- **Automatic Initialization**: JiraApi object created after HTTP authentication success
- **Session Management**: JiraApi instance maintained for entire app session
- **Credentials Integration**: Uses HTTP authentication username/password from HttpAuthDialog
- **Constructor Parameters**: `JiraApi(serverUrl, user, password, mockingModeEnable = false)`
- **Lifecycle Management**: Proper disposal when app closes or authentication changes

### 5. **Settings Dialog (Tools > Options)**
- **Global Configuration**: Configure Jira Server URL and Project key for all new projects
- **Validation**: URL format validation and required field checks
- **Persistent Storage**: Settings saved to `%AppData%\CoverageAnalyzer\appsettings.json`
- **Default Values**: Server: `https://ontrack-internal.amd.com/`, Project: `DCNDVDBG`
- **Current Project Update**: Updates loaded project when settings are changed

### 6. **Main Application Integration**
- **Project Info Display**: Shows Jira Project, Epic and Story in project information bar
- **Format**: `Release: dcn6.0 | Coverage: Functional | Report: dc_core_verif_plan | Type: Individual | CL: 1234567 | 🎫 Project: DCNDVDBG | Epic: dcn6_0_func | Story: dcn6_0_dc_core_verif_plan`
- **Conditional Display**: Only shows Jira info when fields are populated
- **API Access**: `GetJiraApi()` method provides access to JiraApi instance for external operations

### 5. **Event Handling & Validation**
- **TextChanged Events**: Update project settings when user manually edits fields
- **Selection Change Events**: Auto-regenerate when release, report, or coverage type changes
- **Validation**: Fields are included in project creation validation
- **Error Handling**: Comprehensive try-catch blocks with user feedback

## File Changes Made

### Core Model Changes:
1. **ProjectSettings.cs**
   - Added `JiraEpic` and `JiraStory` properties with JsonProperty attributes
   - Added `GenerateJiraFields()` method with intelligent name formatting
   - JSON serialization support for persistence

### UI Changes:
2. **ProjectWizard.xaml**
   - Added complete Jira Configuration section with professional styling
   - Two-column layout for Epic and Story fields
   - Info text and regenerate button
   - Consistent styling with existing wizard theme

3. **ProjectWizard.xaml.cs**
   - Added event handlers: `JiraEpicTextBox_TextChanged`, `JiraStoryTextBox_TextChanged`, `RegenerateJiraButton_Click`
   - Added helper methods: `UpdateJiraFieldsUI()`, `AutoGenerateJiraFields()`
   - Integrated auto-generation into existing selection change events
   - Updated project creation process to store Jira fields

4. **MainWindow.xaml.cs**
   - Updated `ProjectInfoText` display to include Jira fields
   - Added null-safe Jira field display logic

## Usage Workflow

### Automatic Generation:
1. **Create New Project** → Opens ProjectWizard
2. **Select Release** (e.g., "dcn6.0") → Auto-generates Epic prefix
3. **Select Coverage Type** (e.g., "Functional") → Completes Epic: "dcn6_0_func"
4. **Select Report** (e.g., "dce_verif_plan") → Generates Story: "dce"
5. **Fields Update Automatically** as selections change

### Manual Override:
1. **Edit Fields Directly** in the text boxes
2. **Use Regenerate Button** to reset to auto-generated values
3. **Validation** ensures fields are saved with project

### Project Loading:
1. **Load Existing Project** → Jira fields loaded from project.json
2. **Display in Project Info** → Shows in main application status bar
3. **Context Menu Integration** → Ready for Jira ticket creation

## Sample Output

### Example Generated Fields:
```json
{
  "jiraEpic": "dcn6_0_func",
  "jiraStory": "dcn6_0_dc_core_verif_plan",
  // ... other project settings
}
```

### Example Project Info Display:
```
Release: dcn6.0 | Coverage: Functional | Report: dc_core_verif_plan | Type: Individual | CL: 1234567 | 🎫 Epic: dcn6_0_func | Story: dcn6_0_dc_core_verif_plan
```

## Benefits

1. **Consistency**: Standardized Epic and Story naming across all projects
2. **Automation**: Reduces manual entry and errors
3. **Integration**: Seamlessly integrated into existing project workflow
4. **Flexibility**: Supports both auto-generation and manual override
5. **Persistence**: Fields are saved in project files for future reference
6. **Visibility**: Clear display in main application interface

## Next Steps Ready

The infrastructure is now in place to:
1. **Create Jira Tickets**: Use Epic and Story fields for automated ticket creation
2. **Link Issues**: Connect coverage analysis to specific Jira items
3. **Reporting**: Include Jira context in coverage reports
4. **Tracking**: Associate test results with Epic/Story workflow

This implementation provides a solid foundation for full Jira integration while maintaining the existing project workflow.