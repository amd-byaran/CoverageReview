# Coverage Analyzer GUI

A WPF desktop application with a Visual Studio-like interface for analyzing coverage data using AMD's parser DLLs.

## Project Structure

```
CoverageAnalyzerGUI/
├── src/                          # Source code
│   ├── App.xaml                  # Application definition
│   ├── App.xaml.cs               # Application code-behind
│   ├── MainWindow.xaml           # Main window XAML layout
│   ├── MainWindow.xaml.cs        # Main window code-behind
│   ├── Commands/                 # Command implementations
│   │   └── RelayCommand.cs       # Generic relay command
│   ├── ViewModels/               # MVVM view models
│   │   ├── ViewModelBase.cs      # Base view model class
│   │   └── MainWindowViewModel.cs# Main window view model
│   └── Models/                   # Data models (placeholder)
├── include/                      # DLL interop headers
│   ├── CoverageParserInterop.cs  # CoverageParser.dll wrapper
│   ├── ExclusionParserInterop.cs # ExclusionParser.dll wrapper
│   └── FunctionalParserInterop.cs# FunctionalParser.dll wrapper
├── resources/                    # Application resources
│   └── icons/                    # Icon files (placeholder)
├── test/                         # Unit tests
│   └── MainWindowViewModelTests.cs
└── CoverageAnalyzerGUI.csproj    # Project file
```

## Features

### Visual Studio-Like Interface
- **Menu Bar**: File, Edit, View, Tools, Help menus with standard commands
- **Toolbar**: Quick access buttons for common operations
- **Solution Explorer**: Left panel showing project structure and coverage files
- **Main Working Area**: Tabbed interface for different analysis views
- **Output Window**: Bottom panel with categorized output (General, Coverage Analysis, Parser Output, Build, Debug)
- **Status Bar**: Shows current status, parser state, and file count

### Core Functionality
- **Coverage Analysis**: Integration with CoverageParser.dll for parsing Synopsys URG files
- **Exclusion Management**: Integration with ExclusionParser.dll for managing exclusions
- **Functional Coverage**: Integration with FunctionalParser.dll for functional coverage analysis
- **MVVM Architecture**: Clean separation of UI and business logic
- **Asynchronous Operations**: Non-blocking UI during long-running operations

## Parser DLL Integration

### CoverageParser.dll
Handles parsing of Synopsys URG coverage files:
- `hierarchy.txt` - Design hierarchy parsing
- `modinfo.txt` - Module information parsing
- `modlist.txt` - Module list parsing
- `tests.txt` - Test execution data
- `dashboard.txt` - Coverage dashboard data

### ExclusionParser.dll
Manages coverage exclusions:
- XML-based exclusion rules
- Configuration file processing
- 95% test success rate

### FunctionalParser.dll
Processes functional coverage data:
- Coverage group analysis
- Coverage point tracking
- Multi-platform support (Windows/Linux/macOS)

## Building and Running

### Prerequisites
- .NET 8.0 SDK or later
- Windows OS (for WPF)
- Visual Studio 2022 or Visual Studio Code
- Parser DLLs (CoverageParser.dll, ExclusionParser.dll, FunctionalParser.dll)

### Build Instructions
```bash
# Clone or navigate to the project directory
cd CoverageAnalyzerGUI

# Restore dependencies
dotnet restore

# Build the application
dotnet build --configuration Release

# Run the application
dotnet run --configuration Release
```

### Using Visual Studio 2025 Insider
```powershell
# Import Visual Studio environment
Import-VisualStudioEnvironment

# Build using MSBuild
msbuild CoverageAnalyzerGUI.csproj /p:Configuration=Release
```

## Usage

1. **Start the Application**: Run the executable or use `dotnet run`
2. **Load Coverage Data**: Use File → Open Project to select coverage data directory
3. **Parse Data**: Click "Parse Data" toolbar button or use Tools → Parse Coverage Data
4. **Analyze Results**: Use the tabbed interface to view different analysis perspectives
5. **Monitor Progress**: Check the Output window for parsing progress and results

## Commands Available

### File Menu
- New Project - Create a new coverage analysis project
- Open Project - Load existing coverage data
- Save/Save All - Save current analysis

### Edit Menu
- Standard editing commands (Undo, Redo, Cut, Copy, Paste)

### View Menu
- Toggle Solution Explorer visibility
- Toggle Output window visibility
- Toggle Error List visibility

### Tools Menu
- Run Coverage Analysis - Execute full coverage analysis
- Parse Coverage Data - Parse coverage files using DLL parsers
- Options - Configure application settings

## Technical Details

### MVVM Pattern
- **Views**: XAML files defining the UI layout
- **ViewModels**: Business logic and data binding (MainWindowViewModel)
- **Models**: Data structures (to be expanded with parser integration)
- **Commands**: User actions (RelayCommand implementation)

### Asynchronous Design
- Long-running operations use `Task.Run()` to avoid blocking the UI
- `Dispatcher.Invoke()` ensures UI updates occur on the UI thread
- Progress feedback through status bar and output window

### Parser Integration
- P/Invoke wrappers for C++ DLL interfaces
- Safe string marshaling between managed and native code
- Error handling and resource cleanup

## Future Enhancements

1. **Real DLL Integration**: Connect actual parser DLLs with proper exports
2. **Data Visualization**: Charts and graphs for coverage metrics
3. **Export Functionality**: Generate reports in various formats
4. **Plugin Architecture**: Support for additional parser types
5. **Configuration Management**: Persistent settings and preferences

## Testing

Unit tests are included in the `test/` directory:
```bash
# Run tests (requires MSTest framework)
dotnet test
```

## Version Information

- **Framework**: .NET 8.0 Windows
- **UI Technology**: WPF (Windows Presentation Foundation)
- **Architecture**: MVVM (Model-View-ViewModel)
- **Build System**: .NET SDK with MSBuild

## Support

For issues related to the parser DLLs, refer to:
- CoverageParser: PARSER_ARCHITECTURE.md
- ExclusionParser: PROJECT_SUMMARY.md  
- FunctionalParser: DLL_DOCUMENTATION.md

© 2025 AMD Coverage Analysis Team