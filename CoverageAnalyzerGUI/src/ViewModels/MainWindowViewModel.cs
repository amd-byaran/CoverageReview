using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CoverageAnalyzerGUI.Commands;

namespace CoverageAnalyzerGUI.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private string _statusText = "Ready";
        private string _parserStatus = "Not Loaded";
        private int _fileCount = 0;
        private string _outputText = "Coverage Analyzer - Ready\nClick 'Parse Coverage Data' or 'Load Data' to load coverage information...";

        public MainWindowViewModel()
        {
            SolutionExplorerViewModel = new SolutionExplorerViewModel();
            InitializeCommands();
            
            // Initialize with welcome message
            AddToOutput("Welcome to Coverage Analyzer GUI");
            AddToOutput("Ready to parse coverage data from DLL parsers");
            
            // Initialize with light theme
            SetTheme("Light");
        }

        #region Properties

        public SolutionExplorerViewModel SolutionExplorerViewModel { get; }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string ParserStatus
        {
            get => _parserStatus;
            set => SetProperty(ref _parserStatus, value);
        }

        public int FileCount
        {
            get => _fileCount;
            set => SetProperty(ref _fileCount, value);
        }

        public string OutputText
        {
            get => _outputText;
            set => SetProperty(ref _outputText, value);
        }

        #endregion

        #region Commands

        public ICommand NewProjectCommand { get; private set; } = null!;
        public ICommand OpenProjectCommand { get; private set; } = null!;
        public ICommand SaveCommand { get; private set; } = null!;
        public ICommand SaveAllCommand { get; private set; } = null!;
        public ICommand ExitCommand { get; private set; } = null!;
        public ICommand UndoCommand { get; private set; } = null!;
        public ICommand RedoCommand { get; private set; } = null!;
        public ICommand CutCommand { get; private set; } = null!;
        public ICommand CopyCommand { get; private set; } = null!;
        public ICommand PasteCommand { get; private set; } = null!;
        public ICommand ToggleSolutionExplorerCommand { get; private set; } = null!;
        public ICommand ToggleOutputCommand { get; private set; } = null!;
        public ICommand ToggleErrorListCommand { get; private set; } = null!;
        public ICommand RunCoverageAnalysisCommand { get; private set; } = null!;
        public ICommand ParseCoverageDataCommand { get; private set; } = null!;
        public ICommand OptionsCommand { get; private set; } = null!;
        public ICommand AboutCommand { get; private set; } = null!;
        public ICommand SetLightThemeCommand { get; private set; } = null!;
        public ICommand SetDarkThemeCommand { get; private set; } = null!;

        #endregion

        #region Command Implementations

        private void InitializeCommands()
        {
            NewProjectCommand = new RelayCommand(ExecuteNewProject);
            OpenProjectCommand = new RelayCommand(ExecuteOpenProject);
            SaveCommand = new RelayCommand(ExecuteSave);
            SaveAllCommand = new RelayCommand(ExecuteSaveAll);
            ExitCommand = new RelayCommand(ExecuteExit);
            UndoCommand = new RelayCommand(ExecuteUndo);
            RedoCommand = new RelayCommand(ExecuteRedo);
            CutCommand = new RelayCommand(ExecuteCut);
            CopyCommand = new RelayCommand(ExecuteCopy);
            PasteCommand = new RelayCommand(ExecutePaste);
            ToggleSolutionExplorerCommand = new RelayCommand(ExecuteToggleSolutionExplorer);
            ToggleOutputCommand = new RelayCommand(ExecuteToggleOutput);
            ToggleErrorListCommand = new RelayCommand(ExecuteToggleErrorList);
            RunCoverageAnalysisCommand = new RelayCommand(ExecuteRunCoverageAnalysis);
            ParseCoverageDataCommand = new RelayCommand(ExecuteParseCoverageData);
            OptionsCommand = new RelayCommand(ExecuteOptions);
            AboutCommand = new RelayCommand(ExecuteAbout);
            SetLightThemeCommand = new RelayCommand(ExecuteSetLightTheme);
            SetDarkThemeCommand = new RelayCommand(ExecuteSetDarkTheme);
        }

        private void ExecuteNewProject()
        {
            StatusText = "Creating new project...";
            AddToOutput("New Project command executed.");
            // TODO: Implement new project functionality
        }

        private void ExecuteOpenProject()
        {
            StatusText = "Opening project...";
            AddToOutput("Open Project command executed.");
            // TODO: Implement open project functionality using Microsoft.Win32.OpenFileDialog
        }

        private void ExecuteSave()
        {
            StatusText = "Saving...";
            AddToOutput("Save command executed.");
            // TODO: Implement save functionality
        }

        private void ExecuteSaveAll()
        {
            StatusText = "Saving all...";
            AddToOutput("Save All command executed.");
            // TODO: Implement save all functionality
        }

        private void ExecuteExit()
        {
            Application.Current.Shutdown();
        }

        private void ExecuteUndo()
        {
            AddToOutput("Undo command executed.");
            // TODO: Implement undo functionality
        }

        private void ExecuteRedo()
        {
            AddToOutput("Redo command executed.");
            // TODO: Implement redo functionality
        }

        private void ExecuteCut()
        {
            AddToOutput("Cut command executed.");
            // TODO: Implement cut functionality
        }

        private void ExecuteCopy()
        {
            AddToOutput("Copy command executed.");
            // TODO: Implement copy functionality
        }

        private void ExecutePaste()
        {
            AddToOutput("Paste command executed.");
            // TODO: Implement paste functionality
        }

        private void ExecuteToggleSolutionExplorer()
        {
            AddToOutput("Toggle Solution Explorer command executed.");
            // TODO: Implement solution explorer toggle
        }

        private void ExecuteToggleOutput()
        {
            AddToOutput("Toggle Output command executed.");
            // TODO: Implement output window toggle
        }

        private void ExecuteToggleErrorList()
        {
            AddToOutput("Toggle Error List command executed.");
            // TODO: Implement error list toggle
        }

        private void ExecuteRunCoverageAnalysis()
        {
            StatusText = "Running coverage analysis...";
            ParserStatus = "Analyzing";
            AddToOutput("Starting coverage analysis...");
            
            // TODO: Implement coverage analysis using the DLL parsers
            // This will integrate with CoverageParser, ExclusionParser, and FunctionalParser DLLs
            
            // Simulate some work
            System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(2000);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusText = "Analysis complete";
                    ParserStatus = "Ready";
                    FileCount = 5; // Example file count
                    AddToOutput("Coverage analysis completed successfully.");
                });
            });
        }

        private void ExecuteParseCoverageData()
        {
            try
            {
                StatusText = "Parsing coverage data...";
                ParserStatus = "Parsing";
                AddToOutput("=== PARSE COVERAGE DATA BUTTON CLICKED ===");
                AddToOutput("Starting coverage data parsing using DLL parsers...");
                
                // Force UI update
                System.Windows.Application.Current.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                
                AddToOutput("Calling SolutionExplorerViewModel.LoadDataCommand...");
                
                // Execute the load command
                if (SolutionExplorerViewModel.LoadDataCommand.CanExecute(null))
                {
                    SolutionExplorerViewModel.LoadDataCommand.Execute(null);
                    AddToOutput("LoadDataCommand executed successfully");
                }
                else
                {
                    AddToOutput("ERROR: LoadDataCommand.CanExecute returned false");
                }
                
                StatusText = "Parsing complete";
                ParserStatus = "Loaded";
                FileCount = 2; // CoverageParser + FunctionalParser
                AddToOutput("Coverage data parsing completed.");
                AddToOutput("Check Solution Explorer for loaded data.");
                AddToOutput("=== PARSE COVERAGE DATA COMPLETED ===");
            }
            catch (Exception ex)
            {
                StatusText = "Parsing failed";
                ParserStatus = "Error";
                AddToOutput($"ERROR during parsing: {ex.Message}");
                AddToOutput($"Stack trace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine($"ExecuteParseCoverageData error: {ex}");
            }
        }

        private void ExecuteOptions()
        {
            AddToOutput("Options command executed.");
            // TODO: Implement options dialog
        }

        private void ExecuteAbout()
        {
            MessageBox.Show(
                "Coverage Analyzer GUI\n" +
                "Version 1.0.0\n" +
                "Built with WPF for .NET 8\n\n" +
                "Integrates with:\n" +
                "- CoverageParser DLL\n" +
                "- ExclusionParser DLL\n" +
                "- FunctionalParser DLL\n\n" +
                "© 2025 AMD Coverage Analysis Team",
                "About Coverage Analyzer",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ExecuteSetLightTheme()
        {
            SetTheme("Light");
            AddToOutput("Switched to Light theme");
        }

        private void ExecuteSetDarkTheme()
        {
            SetTheme("Dark");
            AddToOutput("Switched to Dark theme");
        }

        private void SetTheme(string themeName)
        {
            try
            {
                // Clear existing theme resources
                Application.Current.Resources.MergedDictionaries.Clear();

                // Create new resource dictionary for the theme
                ResourceDictionary themeDict = new ResourceDictionary();

                if (themeName == "Dark")
                {
                    // Dark theme colors
                    themeDict["WindowBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(45, 45, 48));
                    themeDict["ControlBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(37, 37, 38));
                    themeDict["TextForegroundBrush"] = new SolidColorBrush(Color.FromRgb(241, 241, 241));
                    themeDict["BorderBrush"] = new SolidColorBrush(Color.FromRgb(63, 63, 70));
                    themeDict["MenuBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(45, 45, 48));
                    themeDict["StatusBarBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0, 122, 204));
                }
                else
                {
                    // Light theme colors
                    themeDict["WindowBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    themeDict["ControlBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(246, 246, 246));
                    themeDict["TextForegroundBrush"] = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                    themeDict["BorderBrush"] = new SolidColorBrush(Color.FromRgb(229, 229, 229));
                    themeDict["MenuBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(246, 246, 246));
                    themeDict["StatusBarBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0, 122, 204));
                }

                Application.Current.Resources.MergedDictionaries.Add(themeDict);
                StatusText = $"{themeName} theme applied";
            }
            catch (Exception ex)
            {
                AddToOutput($"Error applying {themeName} theme: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        private void AddToOutput(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            OutputText += $"\n[{timestamp}] {message}";
        }

        #endregion
    }
}