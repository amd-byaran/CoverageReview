# FunctionalCoverageParsers DLL and Library Documentation

## Overview

The FunctionalCoverageParsers library is available as both a Dynamic Link Library (DLL) for Windows and shared libraries for other platforms. This document describes how to build, distribute, and use the library.

## Build Artifacts

### Windows (MSVC)
- **DLL**: `FunctionalCoverageParsers.dll` - Runtime library containing the implementation
- **LIB**: `FunctionalCoverageParsers.lib` - Import library for linking
- **EXP**: `FunctionalCoverageParsers.exp` - Export file (generated automatically)
- **DEF**: `FunctionalCoverageParsers.def` - Module definition file

### Linux/Unix
- **Shared Library**: `libFunctionalCoverageParsers.so` - Shared object library
- **Static Library**: `libFunctionalCoverageParsers_static.a` - Static library

### macOS
- **Shared Library**: `libFunctionalCoverageParsers.dylib` - Dynamic library
- **Static Library**: `libFunctionalCoverageParsers_static.a` - Static library

## Building the Library

### Method 1: Using CMake (Recommended)

```bash
# Create build directory
mkdir build && cd build

# Configure build
cmake -DCMAKE_BUILD_TYPE=Release ..

# Build library
cmake --build . --config Release

# Install (optional)
cmake --install . --prefix /path/to/install
```

### Method 2: Using Visual Studio (Windows)

```batch
# Run the PowerShell build script
PowerShell -ExecutionPolicy Bypass -File build_dll.ps1

# Or use the batch file
build_dll.bat
```

### Method 3: Manual compilation

```bash
# Windows (MSVC)
cl /std:c++17 /EHsc /MD /O2 /DBUILDING_COVERAGE_PARSER_DLL ^
   /I./include src/*.cpp /link /DLL /DEF:FunctionalCoverageParsers.def ^
   /OUT:FunctionalCoverageParsers.dll /IMPLIB:FunctionalCoverageParsers.lib

# Linux (GCC)
g++ -std=c++17 -fPIC -shared -O2 -DBUILDING_COVERAGE_PARSER_DLL ^
    -I./include src/*.cpp -o libFunctionalCoverageParsers.so

# macOS (Clang)
clang++ -std=c++17 -fPIC -shared -O2 -DBUILDING_COVERAGE_PARSER_DLL ^
         -I./include src/*.cpp -o libFunctionalCoverageParsers.dylib
```

## API Reference

### Core Functions

```c
// Library information
const char* get_version_string();
const char* get_library_info();
const char* get_error_string(int result);

// Database management
void* create_coverage_database();
void destroy_coverage_database(void* handle);
int validate_database(void* db_handle);
double calculate_overall_score(void* db_handle);

// Parser creation
void* create_dashboard_parser();
void* create_groups_parser();
void* create_hierarchy_parser();
void* create_modlist_parser();
void* create_assert_parser();
void destroy_parser(void* handle);

// Parsing operations
int parse_coverage_file(void* parser_handle, const char* filename, void* db_handle);

// Query functions
int get_num_groups(void* db_handle);
int get_num_hierarchy_instances(void* db_handle);
int get_num_modules(void* db_handle);
int get_num_asserts(void* db_handle);

// Export functions
int export_coverage_to_xml(void* db_handle, const char* filename);
int export_coverage_to_json(void* db_handle, const char* filename);

// Resource management
int get_memory_usage(uint32_t* total_bytes, uint32_t* num_allocations);
void cleanup_library();
```

## Usage Examples

### C Application

```c
#include "functional_coverage_parser_dll.h"
#include <stdio.h>

int main() {
    // Initialize library
    printf("FunctionalCoverageParsers %s\n", get_version_string());
    
    // Create database and parsers
    void* db = create_coverage_database();
    void* dashboard_parser = create_dashboard_parser();
    void* groups_parser = create_groups_parser();
    
    if (!db || !dashboard_parser || !groups_parser) {
        printf("Failed to create parsers\n");
        return 1;
    }
    
    // Parse files
    int result = parse_coverage_file(dashboard_parser, "dashboard.txt", db);
    if (result != 0) {
        printf("Dashboard parse failed: %s\n", get_error_string(result));
    }
    
    result = parse_coverage_file(groups_parser, "groups.txt", db);
    if (result != 0) {
        printf("Groups parse failed: %s\n", get_error_string(result));
    }
    
    // Get statistics
    double score = calculate_overall_score(db);
    printf("Overall coverage: %.2f%%\n", score);
    
    printf("Groups: %d\n", get_num_groups(db));
    printf("Hierarchy instances: %d\n", get_num_hierarchy_instances(db));
    printf("Modules: %d\n", get_num_modules(db));
    printf("Assertions: %d\n", get_num_asserts(db));
    
    // Export results
    export_coverage_to_xml(db, "coverage_report.xml");
    export_coverage_to_json(db, "coverage_report.json");
    
    // Cleanup
    destroy_parser(dashboard_parser);
    destroy_parser(groups_parser);
    destroy_coverage_database(db);
    cleanup_library();
    
    return 0;
}
```

### C++ Application

```cpp
// You can also use the C++ API directly
#include "functional_coverage_parser.h"

int main() {
    using namespace coverage_parser;
    
    auto db = std::make_unique<CoverageDatabase>();
    
    DashboardParser dashboard_parser;
    GroupsParser groups_parser;
    
    ParserResult result = dashboard_parser.parse("dashboard.txt", *db);
    if (result == ParserResult::SUCCESS) {
        std::cout << "Dashboard parsed successfully\n";
    }
    
    result = groups_parser.parse("groups.txt", *db);
    if (result == ParserResult::SUCCESS) {
        std::cout << "Groups parsed successfully\n";
    }
    
    double score = db->calculate_overall_score();
    std::cout << "Overall coverage: " << score << "%\n";
    
    return 0;
}
```

## Linking Instructions

### Windows (Visual Studio)

1. **Include the header**:
   ```c
   #include "functional_coverage_parser_dll.h"
   ```

2. **Link against the import library**:
   - Add `FunctionalCoverageParsers.lib` to your project's Additional Dependencies
   - Or use pragma: `#pragma comment(lib, "FunctionalCoverageParsers.lib")`

3. **Ensure DLL is available at runtime**:
   - Copy `FunctionalCoverageParsers.dll` to your executable's directory
   - Or add the DLL's directory to your PATH

### Linux (GCC/Clang)

1. **Compile with headers**:
   ```bash
   gcc -I/path/to/headers your_app.c -L/path/to/lib -lFunctionalCoverageParsers -o your_app
   ```

2. **Set library path**:
   ```bash
   export LD_LIBRARY_PATH=/path/to/lib:$LD_LIBRARY_PATH
   ./your_app
   ```

### macOS (Clang)

1. **Compile with headers**:
   ```bash
   clang -I/path/to/headers your_app.c -L/path/to/lib -lFunctionalCoverageParsers -o your_app
   ```

2. **Set library path**:
   ```bash
   export DYLD_LIBRARY_PATH=/path/to/lib:$DYLD_LIBRARY_PATH
   ./your_app
   ```

## CMake Integration

If the library is installed with CMake, you can use it in your project:

```cmake
find_package(FunctionalCoverageParsers REQUIRED)

add_executable(your_app your_app.c)
target_link_libraries(your_app FunctionalCoverageParsers::FunctionalCoverageParsers)
```

## Distribution

### Windows Package Contents
```
FunctionalCoverageParsers/
├── bin/
│   └── FunctionalCoverageParsers.dll
├── lib/
│   ├── FunctionalCoverageParsers.lib
│   ├── FunctionalCoverageParsers.exp
│   └── FunctionalCoverageParsers.def
├── include/
│   ├── coverage_types.h
│   ├── functional_coverage_parser.h
│   └── functional_coverage_parser_dll.h
├── examples/
│   ├── usage_example.c
│   └── CMakeLists.txt
└── docs/
    └── README.md
```

### Linux/Unix Package Contents
```
FunctionalCoverageParsers/
├── lib/
│   ├── libFunctionalCoverageParsers.so
│   └── libFunctionalCoverageParsers_static.a
├── include/
│   ├── coverage_types.h
│   ├── functional_coverage_parser.h
│   └── functional_coverage_parser_dll.h
├── examples/
│   ├── usage_example.c
│   └── Makefile
└── docs/
    └── README.md
```

## Error Handling

All functions return error codes that can be converted to human-readable strings:

```c
int result = parse_coverage_file(parser, "file.txt", db);
if (result != 0) {
    printf("Error: %s\n", get_error_string(result));
}
```

Error codes:
- `0` (PARSER_SUCCESS): Operation successful
- `1` (PARSER_ERROR_FILE_NOT_FOUND): File not found
- `2` (PARSER_ERROR_FILE_ACCESS): File access denied
- `3` (PARSER_ERROR_PARSE_FAILED): Parsing failed
- `4` (PARSER_ERROR_INVALID_FORMAT): Invalid file format
- `5` (PARSER_ERROR_OUT_OF_MEMORY): Out of memory
- `6` (PARSER_ERROR_INVALID_PARAMETER): Invalid parameter

## Thread Safety

The library is not thread-safe by default. If you need to use it from multiple threads:

1. Use separate parser and database instances per thread
2. Or add your own synchronization (mutexes, etc.)
3. The `cleanup_library()` function should only be called once at program exit

## Memory Management

- Always call `destroy_parser()` and `destroy_coverage_database()` for created objects
- Call `cleanup_library()` before program exit to ensure proper cleanup
- Use `get_memory_usage()` to monitor memory consumption
- The library uses smart pointers internally for automatic memory management

## Performance Considerations

- The library is optimized for parsing large coverage files efficiently
- Use the appropriate parser type for each file format
- Consider using static linking for better performance in some scenarios
- The library supports streaming large files without loading everything into memory

## GUI Project Integration

### Windows GUI Applications (WinAPI, MFC, Qt, WPF, etc.)

#### Visual Studio Setup for GUI Projects

1. **Project Properties Configuration**:
   ```
   Project → Properties → C/C++
   ├── General
   │   └── Additional Include Directories: path\to\include
   ├── Preprocessor
   │   └── Preprocessor Definitions: Add UNICODE;_UNICODE (if needed)
   
   Project → Properties → Linker
   ├── General
   │   └── Additional Library Directories: path\to\lib
   ├── Input
   │   └── Additional Dependencies: FunctionalCoverageParsers.lib
   ```

2. **DLL Deployment for GUI Apps**:
   - **Development**: Copy DLL to output directory (Debug/Release)
   - **Distribution**: Include DLL in installer or app folder
   - **Alternative**: Add DLL path to system PATH environment variable

#### Qt GUI Integration Example

```cpp
// Qt MainWindow integration
#include <QtWidgets>
#include "functional_coverage_parser_dll.h"

class CoverageAnalyzerWindow : public QMainWindow {
    Q_OBJECT
    
private:
    void* m_database;
    QProgressBar* m_progressBar;
    QTextEdit* m_logOutput;
    QTableWidget* m_resultsTable;
    
public slots:
    void openCoverageFiles();
    void exportResults();
    void showCoverageStats();

public:
    CoverageAnalyzerWindow(QWidget* parent = nullptr) : QMainWindow(parent) {
        m_database = create_coverage_database();
        setupUI();
    }
    
    ~CoverageAnalyzerWindow() {
        if (m_database) {
            destroy_coverage_database(m_database);
            cleanup_library();
        }
    }
    
private:
    void setupUI() {
        // Create main widget and layout
        auto* centralWidget = new QWidget(this);
        auto* layout = new QVBoxLayout(centralWidget);
        
        // File operations toolbar
        auto* toolbar = addToolBar("File Operations");
        toolbar->addAction("Open Coverage Files", this, &CoverageAnalyzerWindow::openCoverageFiles);
        toolbar->addAction("Export Results", this, &CoverageAnalyzerWindow::exportResults);
        
        // Progress bar for parsing operations
        m_progressBar = new QProgressBar(this);
        m_progressBar->setVisible(false);
        layout->addWidget(m_progressBar);
        
        // Results table
        m_resultsTable = new QTableWidget(this);
        m_resultsTable->setColumnCount(4);
        m_resultsTable->setHorizontalHeaderLabels({"File Type", "Items Count", "Coverage %", "Status"});
        layout->addWidget(m_resultsTable);
        
        // Log output
        m_logOutput = new QTextEdit(this);
        m_logOutput->setMaximumHeight(150);
        layout->addWidget(m_logOutput);
        
        setCentralWidget(centralWidget);
        setWindowTitle("Functional Coverage Analyzer");
        resize(800, 600);
    }
};

void CoverageAnalyzerWindow::openCoverageFiles() {
    QStringList fileNames = QFileDialog::getOpenFileNames(
        this, "Open Coverage Files", "", 
        "Coverage Files (*.txt);;All Files (*.*)");
    
    if (fileNames.isEmpty()) return;
    
    m_progressBar->setVisible(true);
    m_progressBar->setMaximum(fileNames.size());
    m_progressBar->setValue(0);
    
    int row = 0;
    m_resultsTable->setRowCount(fileNames.size());
    
    for (const QString& fileName : fileNames) {
        QFileInfo fileInfo(fileName);
        QString baseName = fileInfo.baseName().toLower();
        
        void* parser = nullptr;
        QString fileType;
        
        // Determine parser type based on filename
        if (baseName.contains("dashboard")) {
            parser = create_dashboard_parser();
            fileType = "Dashboard";
        } else if (baseName.contains("groups")) {
            parser = create_groups_parser();
            fileType = "Groups";
        } else if (baseName.contains("hierarchy")) {
            parser = create_hierarchy_parser();
            fileType = "Hierarchy";
        } else if (baseName.contains("modlist")) {
            parser = create_modlist_parser();
            fileType = "Module List";
        } else if (baseName.contains("assert")) {
            parser = create_assert_parser();
            fileType = "Assertions";
        }
        
        if (parser) {
            int result = parse_coverage_file(parser, fileName.toLocal8Bit().constData(), m_database);
            
            m_resultsTable->setItem(row, 0, new QTableWidgetItem(fileType));
            m_resultsTable->setItem(row, 3, new QTableWidgetItem(result == 0 ? "Success" : "Failed"));
            
            if (result == 0) {
                m_logOutput->append(QString("✓ Parsed %1 successfully").arg(fileInfo.fileName()));
            } else {
                m_logOutput->append(QString("✗ Failed to parse %1: %2").arg(fileInfo.fileName(), get_error_string(result)));
            }
            
            destroy_parser(parser);
        }
        
        m_progressBar->setValue(row + 1);
        QApplication::processEvents(); // Keep UI responsive
        row++;
    }
    
    m_progressBar->setVisible(false);
    showCoverageStats();
}

void CoverageAnalyzerWindow::showCoverageStats() {
    // Update table with counts and overall score
    int groups = get_num_groups(m_database);
    int hierarchy = get_num_hierarchy_instances(m_database);
    int modules = get_num_modules(m_database);
    int asserts = get_num_asserts(m_database);
    double score = calculate_overall_score(m_database);
    
    // Update table cells with counts
    for (int row = 0; row < m_resultsTable->rowCount(); ++row) {
        QString fileType = m_resultsTable->item(row, 0)->text();
        if (fileType == "Groups") {
            m_resultsTable->setItem(row, 1, new QTableWidgetItem(QString::number(groups)));
        } else if (fileType == "Hierarchy") {
            m_resultsTable->setItem(row, 1, new QTableWidgetItem(QString::number(hierarchy)));
        } else if (fileType == "Module List") {
            m_resultsTable->setItem(row, 1, new QTableWidgetItem(QString::number(modules)));
        } else if (fileType == "Assertions") {
            m_resultsTable->setItem(row, 1, new QTableWidgetItem(QString::number(asserts)));
        }
        m_resultsTable->setItem(row, 2, new QTableWidgetItem(QString::number(score, 'f', 2) + "%"));
    }
    
    m_logOutput->append(QString("📊 Overall Coverage Score: %1%").arg(score, 0, 'f', 2));
}

#include "main.moc" // Required for Qt MOC
```

#### WPF/.NET Integration (C++/CLI)

```cpp
// C++/CLI wrapper for .NET integration
#pragma once
#include "functional_coverage_parser_dll.h"

using namespace System;
using namespace System::Collections::Generic;

public ref class CoverageParserWrapper {
private:
    void* m_database;
    Dictionary<String^, void*>^ m_parsers;

public:
    CoverageParserWrapper() {
        m_database = create_coverage_database();
        m_parsers = gcnew Dictionary<String^, void*>();
    }
    
    ~CoverageParserWrapper() {
        this->!CoverageParserWrapper();
    }
    
    !CoverageParserWrapper() {
        for each(KeyValuePair<String^, void*> pair in m_parsers) {
            if (pair.Value) destroy_parser(pair.Value);
        }
        if (m_database) destroy_coverage_database(m_database);
        cleanup_library();
    }
    
    bool ParseFile(String^ fileName, String^ fileType) {
        pin_ptr<const char> nativeFileName = PtrToStringChars(fileName);
        pin_ptr<const char> nativeFileType = PtrToStringChars(fileType);
        
        void* parser = nullptr;
        String^ key = fileType->ToLower();
        
        if (!m_parsers->TryGetValue(key, parser)) {
            if (key == "dashboard") parser = create_dashboard_parser();
            else if (key == "groups") parser = create_groups_parser();
            else if (key == "hierarchy") parser = create_hierarchy_parser();
            else if (key == "modlist") parser = create_modlist_parser();
            else if (key == "assert") parser = create_assert_parser();
            
            if (parser) m_parsers[key] = parser;
        }
        
        if (parser) {
            int result = parse_coverage_file(parser, nativeFileName, m_database);
            return result == 0;
        }
        return false;
    }
    
    double GetOverallScore() {
        return calculate_overall_score(m_database);
    }
    
    int GetGroupCount() { return get_num_groups(m_database); }
    int GetHierarchyCount() { return get_num_hierarchy_instances(m_database); }
    int GetModuleCount() { return get_num_modules(m_database); }
    int GetAssertCount() { return get_num_asserts(m_database); }
    
    bool ExportToXML(String^ fileName) {
        pin_ptr<const char> nativeFileName = PtrToStringChars(fileName);
        return export_coverage_to_xml(m_database, nativeFileName) == 0;
    }
    
    bool ExportToJSON(String^ fileName) {
        pin_ptr<const char> nativeFileName = PtrToStringChars(fileName);
        return export_coverage_to_json(m_database, nativeFileName) == 0;
    }
};
```

#### WinForms Integration Example

```cpp
// WinForms integration
using namespace System::Windows::Forms;

public ref class CoverageAnalyzerForm : public Form {
private:
    CoverageParserWrapper^ m_parser;
    DataGridView^ m_gridView;
    ProgressBar^ m_progressBar;
    TextBox^ m_logTextBox;
    
public:
    CoverageAnalyzerForm() {
        m_parser = gcnew CoverageParserWrapper();
        InitializeComponent();
    }
    
private:
    void InitializeComponent() {
        this->Size = Drawing::Size(800, 600);
        this->Text = "Coverage Analyzer";
        
        // Create menu
        MenuStrip^ menuStrip = gcnew MenuStrip();
        ToolStripMenuItem^ fileMenu = gcnew ToolStripMenuItem("File");
        ToolStripMenuItem^ openItem = gcnew ToolStripMenuItem("Open Coverage Files");
        openItem->Click += gcnew EventHandler(this, &CoverageAnalyzerForm::OpenFiles);
        fileMenu->DropDownItems->Add(openItem);
        menuStrip->Items->Add(fileMenu);
        
        // Create data grid
        m_gridView = gcnew DataGridView();
        m_gridView->Dock = DockStyle::Fill;
        m_gridView->AutoGenerateColumns = false;
        
        // Add columns
        m_gridView->Columns->Add("FileType", "File Type");
        m_gridView->Columns->Add("Count", "Items");
        m_gridView->Columns->Add("Coverage", "Coverage %");
        m_gridView->Columns->Add("Status", "Status");
        
        // Create progress bar
        m_progressBar = gcnew ProgressBar();
        m_progressBar->Dock = DockStyle::Bottom;
        m_progressBar->Visible = false;
        
        // Create log text box
        m_logTextBox = gcnew TextBox();
        m_logTextBox->Multiline = true;
        m_logTextBox->ScrollBars = ScrollBars::Vertical;
        m_logTextBox->Height = 120;
        m_logTextBox->Dock = DockStyle::Bottom;
        
        this->Controls->Add(m_gridView);
        this->Controls->Add(m_logTextBox);
        this->Controls->Add(m_progressBar);
        this->Controls->Add(menuStrip);
        this->MainMenuStrip = menuStrip;
    }
    
    void OpenFiles(Object^ sender, EventArgs^ e) {
        OpenFileDialog^ dialog = gcnew OpenFileDialog();
        dialog->Filter = "Coverage Files (*.txt)|*.txt|All Files (*.*)|*.*";
        dialog->Multiselect = true;
        
        if (dialog->ShowDialog() == DialogResult::OK) {
            m_progressBar->Visible = true;
            m_progressBar->Maximum = dialog->FileNames->Length;
            m_progressBar->Value = 0;
            
            m_gridView->Rows->Clear();
            
            for (int i = 0; i < dialog->FileNames->Length; i++) {
                String^ fileName = dialog->FileNames[i];
                String^ fileType = DetermineFileType(fileName);
                
                bool success = m_parser->ParseFile(fileName, fileType);
                
                array<Object^>^ row = gcnew array<Object^>(4);
                row[0] = fileType;
                row[1] = ""; // Will be filled after parsing
                row[2] = String::Format("{0:F2}%", m_parser->GetOverallScore());
                row[3] = success ? "Success" : "Failed";
                
                m_gridView->Rows->Add(row);
                
                m_logTextBox->AppendText(String::Format("{0} {1}: {2}\r\n",
                    success ? "✓" : "✗", IO::Path::GetFileName(fileName),
                    success ? "Success" : "Failed"));
                
                m_progressBar->Value = i + 1;
                Application::DoEvents();
            }
            
            // Update counts
            UpdateCounts();
            m_progressBar->Visible = false;
        }
    }
    
    String^ DetermineFileType(String^ fileName) {
        String^ baseName = IO::Path::GetFileNameWithoutExtension(fileName)->ToLower();
        if (baseName->Contains("dashboard")) return "dashboard";
        if (baseName->Contains("groups")) return "groups";
        if (baseName->Contains("hierarchy")) return "hierarchy";
        if (baseName->Contains("modlist")) return "modlist";
        if (baseName->Contains("assert")) return "assert";
        return "unknown";
    }
    
    void UpdateCounts() {
        int groups = m_parser->GetGroupCount();
        int hierarchy = m_parser->GetHierarchyCount();
        int modules = m_parser->GetModuleCount();
        int asserts = m_parser->GetAssertCount();
        
        for (int i = 0; i < m_gridView->Rows->Count; i++) {
            String^ fileType = safe_cast<String^>(m_gridView->Rows[i]->Cells[0]->Value);
            if (fileType == "groups") m_gridView->Rows[i]->Cells[1]->Value = groups;
            else if (fileType == "hierarchy") m_gridView->Rows[i]->Cells[1]->Value = hierarchy;
            else if (fileType == "modlist") m_gridView->Rows[i]->Cells[1]->Value = modules;
            else if (fileType == "assert") m_gridView->Rows[i]->Cells[1]->Value = asserts;
        }
    }
};
```

### GUI-Specific Considerations

#### Threading in GUI Applications

```cpp
// Example: Background parsing to keep UI responsive
class BackgroundParser : public QThread {
    Q_OBJECT
    
signals:
    void parseComplete(const QString& fileName, bool success);
    void progressUpdate(int current, int total);
    
public slots:
    void parseFiles(const QStringList& fileNames) {
        void* database = create_coverage_database();
        
        for (int i = 0; i < fileNames.size(); ++i) {
            // Determine parser type and parse file
            // ... parsing logic ...
            
            emit progressUpdate(i + 1, fileNames.size());
            emit parseComplete(fileNames[i], success);
        }
        
        destroy_coverage_database(database);
    }
};
```

#### Memory Management in GUI Apps

```cpp
// RAII wrapper for automatic cleanup
class CoverageParserRAII {
private:
    void* m_database;
    std::vector<void*> m_parsers;
    
public:
    CoverageParserRAII() : m_database(create_coverage_database()) {}
    
    ~CoverageParserRAII() {
        for (void* parser : m_parsers) {
            if (parser) destroy_parser(parser);
        }
        if (m_database) destroy_coverage_database(m_database);
        cleanup_library();
    }
    
    void* getDatabase() const { return m_database; }
    
    void* createParser(const std::string& type) {
        void* parser = nullptr;
        if (type == "dashboard") parser = create_dashboard_parser();
        else if (type == "groups") parser = create_groups_parser();
        // ... other types ...
        
        if (parser) m_parsers.push_back(parser);
        return parser;
    }
};
```

## Troubleshooting

### Common Issues

1. **DLL not found**: Ensure the DLL is in PATH or same directory as executable
2. **Import library not found**: Check that the .lib file is in the linker's library path
3. **Version mismatch**: Ensure the DLL and headers are from the same version
4. **Memory leaks**: Always call cleanup functions in proper order
5. **GUI freezing**: Use background threads for file parsing operations
6. **Unicode issues**: Ensure consistent character encoding (UTF-8/UTF-16)

### Debug Build

For debugging, build with debug symbols:
```bash
cmake -DCMAKE_BUILD_TYPE=Debug ..
```

This enables additional error checking and debug output.

### GUI Debugging Tips

1. **Use debug output**: Enable console window in Windows GUI apps for debug output
2. **Progress indicators**: Always show progress for long operations
3. **Error dialogs**: Display user-friendly error messages from `get_error_string()`
4. **Memory monitoring**: Use `get_memory_usage()` to detect memory issues
5. **Thread safety**: Ensure GUI updates happen on the main thread only

## Additional GUI Framework Examples

### MFC (Microsoft Foundation Classes) Integration

```cpp
// MFC Dialog-based application
class CCoverageAnalyzerDlg : public CDialogEx {
private:
    void* m_pDatabase;
    CListCtrl m_listCtrl;
    CProgressCtrl m_progressCtrl;
    CEdit m_logEdit;

public:
    CCoverageAnalyzerDlg(CWnd* pParent = nullptr) : CDialogEx(IDD_COVERAGE_ANALYZER_DIALOG, pParent) {
        m_pDatabase = create_coverage_database();
    }
    
    virtual ~CCoverageAnalyzerDlg() {
        if (m_pDatabase) {
            destroy_coverage_database(m_pDatabase);
            cleanup_library();
        }
    }

protected:
    virtual void DoDataExchange(CDataExchange* pDX) {
        CDialogEx::DoDataExchange(pDX);
        DDX_Control(pDX, IDC_LIST_RESULTS, m_listCtrl);
        DDX_Control(pDX, IDC_PROGRESS, m_progressCtrl);
        DDX_Control(pDX, IDC_EDIT_LOG, m_logEdit);
    }

    virtual BOOL OnInitDialog() {
        CDialogEx::OnInitDialog();
        
        // Initialize list control
        m_listCtrl.SetExtendedStyle(LVS_EX_FULLROWSELECT | LVS_EX_GRIDLINES);
        m_listCtrl.InsertColumn(0, _T("File Type"), LVCFMT_LEFT, 100);
        m_listCtrl.InsertColumn(1, _T("Items"), LVCFMT_RIGHT, 80);
        m_listCtrl.InsertColumn(2, _T("Coverage %"), LVCFMT_RIGHT, 100);
        m_listCtrl.InsertColumn(3, _T("Status"), LVCFMT_LEFT, 100);
        
        return TRUE;
    }

    afx_msg void OnBnClickedButtonOpen() {
        CFileDialog dlg(TRUE, _T("txt"), NULL, 
            OFN_ALLOWMULTISELECT | OFN_FILEMUSTEXIST,
            _T("Coverage Files (*.txt)|*.txt|All Files (*.*)|*.*||"));
        
        if (dlg.DoModal() == IDOK) {
            POSITION pos = dlg.GetStartPosition();
            std::vector<CString> fileNames;
            
            while (pos != NULL) {
                fileNames.push_back(dlg.GetNextPathName(pos));
            }
            
            ParseFiles(fileNames);
        }
    }

private:
    void ParseFiles(const std::vector<CString>& fileNames) {
        m_progressCtrl.SetRange(0, static_cast<int>(fileNames.size()));
        m_progressCtrl.SetPos(0);
        m_listCtrl.DeleteAllItems();
        
        for (size_t i = 0; i < fileNames.size(); ++i) {
            CString fileName = fileNames[i];
            CString fileType = DetermineFileType(fileName);
            
            void* parser = CreateParserForType(fileType);
            bool success = false;
            
            if (parser) {
                CT2A asciiFileName(fileName);
                int result = parse_coverage_file(parser, asciiFileName, m_pDatabase);
                success = (result == 0);
                destroy_parser(parser);
            }
            
            // Add item to list
            int item = m_listCtrl.InsertItem(static_cast<int>(i), fileType);
            m_listCtrl.SetItemText(item, 2, success ? _T("Success") : _T("Failed"));
            
            // Update log
            CString logMsg;
            logMsg.Format(_T("%s %s: %s\r\n"), 
                success ? _T("✓") : _T("✗"),
                PathFindFileName(fileName),
                success ? _T("Success") : _T("Failed"));
            
            CString currentLog;
            m_logEdit.GetWindowText(currentLog);
            m_logEdit.SetWindowText(currentLog + logMsg);
            
            m_progressCtrl.SetPos(static_cast<int>(i + 1));
            
            // Keep UI responsive
            MSG msg;
            while (PeekMessage(&msg, NULL, 0, 0, PM_REMOVE)) {
                TranslateMessage(&msg);
                DispatchMessage(&msg);
            }
        }
        
        UpdateCounts();
    }
    
    CString DetermineFileType(const CString& fileName) {
        CString baseName = PathFindFileName(fileName);
        baseName.MakeLower();
        
        if (baseName.Find(_T("dashboard")) != -1) return _T("Dashboard");
        if (baseName.Find(_T("groups")) != -1) return _T("Groups");
        if (baseName.Find(_T("hierarchy")) != -1) return _T("Hierarchy");
        if (baseName.Find(_T("modlist")) != -1) return _T("Module List");
        if (baseName.Find(_T("assert")) != -1) return _T("Assertions");
        return _T("Unknown");
    }
    
    void* CreateParserForType(const CString& fileType) {
        if (fileType == _T("Dashboard")) return create_dashboard_parser();
        if (fileType == _T("Groups")) return create_groups_parser();
        if (fileType == _T("Hierarchy")) return create_hierarchy_parser();
        if (fileType == _T("Module List")) return create_modlist_parser();
        if (fileType == _T("Assertions")) return create_assert_parser();
        return nullptr;
    }
    
    void UpdateCounts() {
        double score = calculate_overall_score(m_pDatabase);
        
        for (int i = 0; i < m_listCtrl.GetItemCount(); ++i) {
            CString fileType = m_listCtrl.GetItemText(i, 0);
            int count = 0;
            
            if (fileType == _T("Groups")) count = get_num_groups(m_pDatabase);
            else if (fileType == _T("Hierarchy")) count = get_num_hierarchy_instances(m_pDatabase);
            else if (fileType == _T("Module List")) count = get_num_modules(m_pDatabase);
            else if (fileType == _T("Assertions")) count = get_num_asserts(m_pDatabase);
            
            CString countStr;
            countStr.Format(_T("%d"), count);
            m_listCtrl.SetItemText(i, 1, countStr);
            
            CString scoreStr;
            scoreStr.Format(_T("%.2f%%"), score);
            m_listCtrl.SetItemText(i, 2, scoreStr);
        }
    }

    DECLARE_MESSAGE_MAP()
};

BEGIN_MESSAGE_MAP(CCoverageAnalyzerDlg, CDialogEx)
    ON_BN_CLICKED(IDC_BUTTON_OPEN, &CCoverageAnalyzerDlg::OnBnClickedButtonOpen)
END_MESSAGE_MAP()
```

### GTK+ (Linux GUI) Integration

```cpp
// GTK+ integration example
#include <gtk/gtk.h>
#include "functional_coverage_parser_dll.h"

typedef struct {
    void* database;
    GtkWidget* treeview;
    GtkWidget* progressbar;
    GtkWidget* textview;
    GtkListStore* store;
} AppData;

static void on_open_files(GtkWidget* widget, gpointer data) {
    AppData* app = (AppData*)data;
    
    GtkWidget* dialog = gtk_file_chooser_dialog_new(
        "Open Coverage Files",
        GTK_WINDOW(gtk_widget_get_toplevel(widget)),
        GTK_FILE_CHOOSER_ACTION_OPEN,
        "_Cancel", GTK_RESPONSE_CANCEL,
        "_Open", GTK_RESPONSE_ACCEPT,
        NULL);
    
    gtk_file_chooser_set_select_multiple(GTK_FILE_CHOOSER(dialog), TRUE);
    
    // Add file filter
    GtkFileFilter* filter = gtk_file_filter_new();
    gtk_file_filter_set_name(filter, "Coverage Files");
    gtk_file_filter_add_pattern(filter, "*.txt");
    gtk_file_chooser_add_filter(GTK_FILE_CHOOSER(dialog), filter);
    
    if (gtk_dialog_run(GTK_DIALOG(dialog)) == GTK_RESPONSE_ACCEPT) {
        GSList* filenames = gtk_file_chooser_get_filenames(GTK_FILE_CHOOSER(dialog));
        GSList* l;
        int total_files = g_slist_length(filenames);
        int current_file = 0;
        
        gtk_list_store_clear(app->store);
        gtk_progress_bar_set_fraction(GTK_PROGRESS_BAR(app->progressbar), 0.0);
        gtk_widget_show(app->progressbar);
        
        for (l = filenames; l != NULL; l = l->next) {
            char* filename = (char*)l->data;
            char* basename = g_path_get_basename(filename);
            
            // Determine file type
            char* file_type = determine_file_type(basename);
            void* parser = create_parser_for_type(file_type);
            
            bool success = false;
            if (parser) {
                int result = parse_coverage_file(parser, filename, app->database);
                success = (result == 0);
                destroy_parser(parser);
            }
            
            // Add to tree view
            GtkTreeIter iter;
            gtk_list_store_append(app->store, &iter);
            gtk_list_store_set(app->store, &iter,
                0, file_type,
                1, 0, // Will be updated later
                2, 0.0, // Will be updated later  
                3, success ? "Success" : "Failed",
                -1);
            
            // Update log
            GtkTextBuffer* buffer = gtk_text_view_get_buffer(GTK_TEXT_VIEW(app->textview));
            GtkTextIter end;
            gtk_text_buffer_get_end_iter(buffer, &end);
            
            char log_msg[256];
            snprintf(log_msg, sizeof(log_msg), "%s %s: %s\n", 
                success ? "✓" : "✗", basename, success ? "Success" : "Failed");
            gtk_text_buffer_insert(buffer, &end, log_msg, -1);
            
            // Update progress
            current_file++;
            gtk_progress_bar_set_fraction(GTK_PROGRESS_BAR(app->progressbar), 
                (double)current_file / total_files);
            
            // Process pending events
            while (gtk_events_pending()) {
                gtk_main_iteration();
            }
            
            g_free(basename);
            g_free(filename);
        }
        
        g_slist_free(filenames);
        update_counts(app);
        gtk_widget_hide(app->progressbar);
    }
    
    gtk_widget_destroy(dialog);
}

static void update_counts(AppData* app) {
    int groups = get_num_groups(app->database);
    int hierarchy = get_num_hierarchy_instances(app->database);
    int modules = get_num_modules(app->database);
    int asserts = get_num_asserts(app->database);
    double score = calculate_overall_score(app->database);
    
    GtkTreeIter iter;
    gboolean valid = gtk_tree_model_get_iter_first(GTK_TREE_MODEL(app->store), &iter);
    
    while (valid) {
        char* file_type;
        gtk_tree_model_get(GTK_TREE_MODEL(app->store), &iter, 0, &file_type, -1);
        
        int count = 0;
        if (strcmp(file_type, "Groups") == 0) count = groups;
        else if (strcmp(file_type, "Hierarchy") == 0) count = hierarchy;
        else if (strcmp(file_type, "Module List") == 0) count = modules;
        else if (strcmp(file_type, "Assertions") == 0) count = asserts;
        
        gtk_list_store_set(app->store, &iter, 1, count, 2, score, -1);
        
        g_free(file_type);
        valid = gtk_tree_model_iter_next(GTK_TREE_MODEL(app->store), &iter);
    }
}

int main(int argc, char* argv[]) {
    gtk_init(&argc, &argv);
    
    AppData app;
    app.database = create_coverage_database();
    
    // Create main window
    GtkWidget* window = gtk_window_new(GTK_WINDOW_TOPLEVEL);
    gtk_window_set_title(GTK_WINDOW(window), "Coverage Analyzer");
    gtk_window_set_default_size(GTK_WINDOW(window), 800, 600);
    
    // Create menu bar
    GtkWidget* menubar = gtk_menu_bar_new();
    GtkWidget* file_menu = gtk_menu_new();
    GtkWidget* file_mi = gtk_menu_item_new_with_label("File");
    GtkWidget* open_mi = gtk_menu_item_new_with_label("Open Coverage Files");
    
    gtk_menu_item_set_submenu(GTK_MENU_ITEM(file_mi), file_menu);
    gtk_menu_shell_append(GTK_MENU_SHELL(file_menu), open_mi);
    gtk_menu_shell_append(GTK_MENU_SHELL(menubar), file_mi);
    
    g_signal_connect(open_mi, "activate", G_CALLBACK(on_open_files), &app);
    
    // Create tree view for results
    app.store = gtk_list_store_new(4, G_TYPE_STRING, G_TYPE_INT, G_TYPE_DOUBLE, G_TYPE_STRING);
    app.treeview = gtk_tree_view_new_with_model(GTK_TREE_MODEL(app.store));
    
    // Add columns
    GtkCellRenderer* renderer = gtk_cell_renderer_text_new();
    gtk_tree_view_insert_column_with_attributes(GTK_TREE_VIEW(app.treeview),
        -1, "File Type", renderer, "text", 0, NULL);
    gtk_tree_view_insert_column_with_attributes(GTK_TREE_VIEW(app.treeview),
        -1, "Items", renderer, "text", 1, NULL);
    gtk_tree_view_insert_column_with_attributes(GTK_TREE_VIEW(app.treeview),
        -1, "Coverage %", renderer, "text", 2, NULL);
    gtk_tree_view_insert_column_with_attributes(GTK_TREE_VIEW(app.treeview),
        -1, "Status", renderer, "text", 3, NULL);
    
    // Create progress bar
    app.progressbar = gtk_progress_bar_new();
    gtk_widget_set_no_show_all(app.progressbar, TRUE);
    
    // Create text view for log
    app.textview = gtk_text_view_new();
    gtk_text_view_set_editable(GTK_TEXT_VIEW(app.textview), FALSE);
    
    // Layout
    GtkWidget* vbox = gtk_box_new(GTK_ORIENTATION_VERTICAL, 0);
    GtkWidget* scrolled1 = gtk_scrolled_window_new(NULL, NULL);
    GtkWidget* scrolled2 = gtk_scrolled_window_new(NULL, NULL);
    
    gtk_container_add(GTK_CONTAINER(scrolled1), app.treeview);
    gtk_container_add(GTK_CONTAINER(scrolled2), app.textview);
    gtk_widget_set_size_request(scrolled2, -1, 150);
    
    gtk_box_pack_start(GTK_BOX(vbox), menubar, FALSE, FALSE, 0);
    gtk_box_pack_start(GTK_BOX(vbox), app.progressbar, FALSE, FALSE, 0);
    gtk_box_pack_start(GTK_BOX(vbox), scrolled1, TRUE, TRUE, 0);
    gtk_box_pack_start(GTK_BOX(vbox), scrolled2, FALSE, FALSE, 0);
    
    gtk_container_add(GTK_CONTAINER(window), vbox);
    
    g_signal_connect(window, "destroy", G_CALLBACK(gtk_main_quit), NULL);
    
    gtk_widget_show_all(window);
    gtk_main();
    
    // Cleanup
    destroy_coverage_database(app.database);
    cleanup_library();
    
    return 0;
}
```

## GUI Deployment Considerations

### Windows Deployment

#### Installer Creation (Using WiX Toolset)

```xml
<!-- Product.wxs - WiX installer configuration -->
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://schemas.microsoft.com/wix/2006/wi">
  <Product Id="*" Name="Coverage Analyzer" Language="1033" Version="1.0.0.0" 
           Manufacturer="Advanced Micro Devices Inc." UpgradeCode="YOUR-GUID-HERE">
    
    <Package InstallerVersion="200" Compressed="yes" InstallScope="perMachine" />
    <MajorUpgrade DowngradeErrorMessage="A newer version is already installed." />
    <MediaTemplate EmbedCab="yes" />

    <Feature Id="ProductFeature" Title="Coverage Analyzer" Level="1">
      <ComponentGroupRef Id="ProductComponents" />
    </Feature>
  </Product>

  <Fragment>
    <Directory Id="TARGETDIR" Name="SourceDir">
      <Directory Id="ProgramFilesFolder">
        <Directory Id="INSTALLFOLDER" Name="Coverage Analyzer" />
      </Directory>
    </Directory>
  </Fragment>

  <Fragment>
    <ComponentGroup Id="ProductComponents" Directory="INSTALLFOLDER">
      <!-- Main executable -->
      <Component Id="MainExecutable" Guid="YOUR-GUID-HERE">
        <File Id="CoverageAnalyzer.exe" Source="Release\CoverageAnalyzer.exe" KeyPath="yes">
          <Shortcut Id="StartMenuShortcut" Directory="ProgramMenuFolder" 
                    Name="Coverage Analyzer" WorkingDirectory="INSTALLFOLDER" 
                    Icon="app.ico" IconIndex="0" Advertise="yes" />
        </File>
      </Component>
      
      <!-- DLL and dependencies -->
      <Component Id="ParserDLL" Guid="YOUR-GUID-HERE">
        <File Id="FunctionalCoverageParsers.dll" 
              Source="lib\FunctionalCoverageParsers.dll" KeyPath="yes" />
      </Component>
      
      <!-- Visual C++ Redistributable (if needed) -->
      <Component Id="VCRedist" Guid="YOUR-GUID-HERE">
        <File Id="vcredist_x64.exe" Source="redist\vcredist_x64.exe" KeyPath="yes" />
      </Component>
    </ComponentGroup>
  </Fragment>
</Wix>
```

#### NSIS Installer Alternative

```nsis
; CoverageAnalyzer.nsi - NSIS installer script
!define PRODUCT_NAME "Coverage Analyzer"
!define PRODUCT_VERSION "1.0.0"
!define PRODUCT_PUBLISHER "Advanced Micro Devices Inc."

Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "CoverageAnalyzer_Setup.exe"
InstallDir "$PROGRAMFILES\${PRODUCT_NAME}"

Section "MainSection" SEC01
  SetOutPath "$INSTDIR"
  File "Release\CoverageAnalyzer.exe"
  File "lib\FunctionalCoverageParsers.dll"
  
  ; Create shortcuts
  CreateDirectory "$SMPROGRAMS\${PRODUCT_NAME}"
  CreateShortCut "$SMPROGRAMS\${PRODUCT_NAME}\${PRODUCT_NAME}.lnk" "$INSTDIR\CoverageAnalyzer.exe"
  CreateShortCut "$DESKTOP\${PRODUCT_NAME}.lnk" "$INSTDIR\CoverageAnalyzer.exe"
  
  ; Register uninstaller
  WriteUninstaller "$INSTDIR\uninstall.exe"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}" \
                   "DisplayName" "${PRODUCT_NAME}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}" \
                   "UninstallString" "$INSTDIR\uninstall.exe"
SectionEnd

Section "Uninstall"
  Delete "$INSTDIR\CoverageAnalyzer.exe"
  Delete "$INSTDIR\FunctionalCoverageParsers.dll"
  Delete "$INSTDIR\uninstall.exe"
  
  Delete "$SMPROGRAMS\${PRODUCT_NAME}\${PRODUCT_NAME}.lnk"
  Delete "$DESKTOP\${PRODUCT_NAME}.lnk"
  RMDir "$SMPROGRAMS\${PRODUCT_NAME}"
  RMDir "$INSTDIR"
  
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}"
SectionEnd
```

### CMake GUI Project Configuration

```cmake
# CMakeLists.txt for GUI application
cmake_minimum_required(VERSION 3.15)
project(CoverageAnalyzerGUI)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Find required packages
find_package(Qt5 REQUIRED COMPONENTS Core Widgets)

# Include FunctionalCoverageParsers
find_package(FunctionalCoverageParsers REQUIRED)

# Source files
set(SOURCES
    main.cpp
    CoverageAnalyzerWindow.cpp
    CoverageAnalyzerWindow.h
)

# Qt MOC files
qt5_wrap_cpp(MOC_SOURCES CoverageAnalyzerWindow.h)

# Create executable
add_executable(CoverageAnalyzerGUI ${SOURCES} ${MOC_SOURCES})

# Link libraries
target_link_libraries(CoverageAnalyzerGUI 
    Qt5::Core 
    Qt5::Widgets
    FunctionalCoverageParsers::FunctionalCoverageParsers
)

# Windows-specific settings
if(WIN32)
    # Use Windows subsystem for GUI app
    set_target_properties(CoverageAnalyzerGUI PROPERTIES WIN32_EXECUTABLE TRUE)
    
    # Copy DLL to output directory
    add_custom_command(TARGET CoverageAnalyzerGUI POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
        $<TARGET_FILE:FunctionalCoverageParsers::FunctionalCoverageParsers>
        $<TARGET_FILE_DIR:CoverageAnalyzerGUI>
    )
endif()

# Installation
install(TARGETS CoverageAnalyzerGUI
    RUNTIME DESTINATION bin
)

if(WIN32)
    install(FILES $<TARGET_FILE:FunctionalCoverageParsers::FunctionalCoverageParsers>
        DESTINATION bin
    )
endif()
```

This comprehensive documentation now covers everything needed to integrate the FunctionalCoverageParsers DLL/LIB into GUI projects across multiple frameworks and platforms, including deployment considerations and real-world examples.