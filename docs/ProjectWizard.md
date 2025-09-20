# Coverage Analyzer - Project Wizard Integration

This document describes the new project wizard functionality that has been integrated into the Coverage Analyzer application.

## 🎯 Overview

The Coverage Analyzer now includes a comprehensive project wizard that allows users to:
- Create new projects with database integration
- Configure SSH file copying from remote servers
- Save and load project settings
- Manage project-specific coverage data

## 🚀 Features

### New Project Creation Wizard

The project wizard guides users through a 7-step process:

1. **Project Setup**
   - Enter project name
   - Select local folder for project storage

2. **Database Connection**
   - Connect to PostgreSQL database using DatabaseReader.dll
   - Validate connection and list available projects

3. **Project Selection**
   - Choose from available database projects
   - Projects are loaded dynamically from the database

4. **Release Selection**
   - Select from releases associated with the chosen project
   - Releases are filtered by project ID

5. **Coverage Type**
   - Choose between Functional Coverage or Code Coverage
   - Affects which reports and changelists are available

6. **Changelist Selection**
   - Select from available changelists for the chosen release and coverage type
   - Report path is automatically generated

7. **SSH Configuration**
   - Configure SSH host and username for file copying
   - Files are copied from the remote report path to local project storage

### Project Management

- **Save/Load Projects**: Project settings are stored in JSON format (`project.json`)
- **File Organization**: Each project has its own folder structure:
  ```
  ProjectFolder/
  ├── project.json         # Project settings
  └── data/               # Coverage data files
      ├── hierarchy.txt
      ├── coverage.txt
      └── *.txt files
  ```

## 📦 Dependencies

The project wizard uses several new dependencies:

- **DatabaseReader.dll**: PostgreSQL database connectivity for coverage data
- **SSH.NET**: SSH file transfer functionality
- **Newtonsoft.Json**: JSON serialization for project settings
- **System.Drawing.Common**: UI components support

## 🔧 Technical Implementation

### Key Classes

- **`ProjectSettings`**: Data model for project configuration
- **`DatabaseProject`**: Represents database project information
- **`DatabaseRelease`**: Represents database release information
- **`ProjectWizard`**: Main wizard window with step-by-step UI

### Database Integration

The wizard integrates with the DatabaseReader.dll assembly:
- `DcPgConn.GetAllProjects()`: Retrieves available projects
- `DcPgConn.GetAllReleases()`: Retrieves available releases
- `DcPgConn.GetReportPath()`: Generates report file paths
- `DcPgConn.GetChangelistsForReport()`: Gets changelists for reports

### SSH File Transfer

Files are copied from remote servers using SSH.NET:
- Connects to specified SSH host with username
- Lists *.txt files in the report directory
- Downloads files with progress tracking
- Shows progress bar with file count and current file

## 🎮 Usage

### Creating a New Project

1. Click **File → New Project** in the main menu
2. Follow the wizard steps:
   - Enter project name and select folder
   - Connect to database
   - Select project, release, and coverage type
   - Choose changelist and configure SSH
3. Click **Create Project** to copy files and save settings

### Opening an Existing Project

1. Click **File → Open Project** in the main menu
2. Select the folder containing `project.json`
3. The project loads automatically with its saved settings

### Project Data

- Coverage data files are stored in the `data/` subfolder
- The main window title shows the current project name
- Tree view displays hierarchy from project-specific data files

## 🔐 Security Considerations

- SSH connections require proper authentication setup
- Database connections should use secure connection strings
- Project files are stored locally and may contain sensitive paths

## 🚧 Future Enhancements

Planned improvements:
- SSH key-based authentication
- Background file synchronization
- Multiple database profile support
- Project templates and presets
- Enhanced error handling and retry logic

## 📝 Project File Format

```json
{
  "projectName": "MyProject",
  "projectFolderPath": "C:\\Projects\\MyProject",
  "selectedDatabaseProject": {
    "id": 1,
    "name": "gpu_verification",
    "description": ""
  },
  "selectedRelease": {
    "id": 5,
    "name": "v2.1.0",
    "projectId": 1,
    "createdAt": "2025-09-16T12:00:00"
  },
  "coverageType": "functional",
  "selectedChangelist": "12345678",
  "reportPath": "/remote/reports/gpu_verification/v2.1.0/functional/12345678",
  "createdAt": "2025-09-16T17:30:00",
  "lastModified": "2025-09-16T17:30:00",
  "sshHost": "coverage-server.amd.com",
  "sshUsername": "user"
}
```

This integration provides a complete workflow for managing coverage analysis projects with database connectivity and remote file access.