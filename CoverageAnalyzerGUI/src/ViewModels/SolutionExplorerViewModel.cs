using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CoverageAnalyzerGUI.Commands;
using CoverageAnalyzerGUI.Models;
using CoverageAnalyzerGUI.Services;

namespace CoverageAnalyzerGUI.ViewModels
{
    public class SolutionExplorerViewModel : ViewModelBase
    {
        private readonly CoverageParserService _parserService;
        private ObservableCollection<TreeViewItemViewModel> _treeItems;

        public SolutionExplorerViewModel()
        {
            _parserService = new CoverageParserService();
            _treeItems = new ObservableCollection<TreeViewItemViewModel>();
            
            LoadDataCommand = new RelayCommand(ExecuteLoadData);
            RefreshCommand = new RelayCommand(ExecuteRefresh);
            
            // Initialize with placeholder
            InitializePlaceholder();
        }

        public ObservableCollection<TreeViewItemViewModel> TreeItems
        {
            get => _treeItems;
            set => SetProperty(ref _treeItems, value);
        }

        public ICommand LoadDataCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }

        private void InitializePlaceholder()
        {
            var rootItem = new TreeViewItemViewModel("Coverage Analysis", null);
            var placeholderItem = new TreeViewItemViewModel("Click 'Parse Coverage Data' to load data", null);
            rootItem.Children.Add(placeholderItem);
            
            TreeItems.Clear();
            TreeItems.Add(rootItem);
        }

        private void ExecuteLoadData()
        {
            System.Diagnostics.Debug.WriteLine("=== ExecuteLoadData CALLED ===");
            
            try
            {
                System.Diagnostics.Debug.WriteLine("Setting loading state...");
                
                // Show loading state
                var loadingItem = new TreeViewItemViewModel("Loading coverage data...", null);
                TreeItems.Clear();
                TreeItems.Add(loadingItem);
                
                System.Diagnostics.Debug.WriteLine("Loading state set, initializing parsers...");

                // Initialize parsers
                string coverageDataDir = @"C:\Users\byaran\OneDrive - Advanced Micro Devices Inc\programming\coverage_analyzer\CoverageParser\data\code";
                string functionalDataDir = @"C:\Users\byaran\OneDrive - Advanced Micro Devices Inc\programming\coverage_analyzer\FunctionalCoverageParsers";
                
                System.Diagnostics.Debug.WriteLine($"Coverage directory: {coverageDataDir}");
                System.Diagnostics.Debug.WriteLine($"Functional directory: {functionalDataDir}");
                System.Diagnostics.Debug.WriteLine("Checking if directories exist...");
                
                bool coverageExists = System.IO.Directory.Exists(coverageDataDir);
                bool functionalExists = System.IO.Directory.Exists(functionalDataDir);
                
                System.Diagnostics.Debug.WriteLine($"Coverage dir exists: {coverageExists}");
                System.Diagnostics.Debug.WriteLine($"Functional dir exists: {functionalExists}");

                bool initialized = _parserService.InitializeParsers(coverageDataDir, functionalDataDir);
                System.Diagnostics.Debug.WriteLine($"Parser initialization result: {initialized}");
                
                if (!initialized)
                {
                    System.Diagnostics.Debug.WriteLine("Parser initialization failed!");
                    var errorItem = new TreeViewItemViewModel("Error: Could not initialize parsers", null);
                    errorItem.Children.Add(new TreeViewItemViewModel("Check if hierarchy.txt files exist", null));
                    errorItem.Children.Add(new TreeViewItemViewModel($"Coverage: {coverageDataDir}", null));
                    errorItem.Children.Add(new TreeViewItemViewModel($"Functional: {functionalDataDir}", null));
                    errorItem.Children.Add(new TreeViewItemViewModel($"Coverage exists: {coverageExists}", null));
                    errorItem.Children.Add(new TreeViewItemViewModel($"Functional exists: {functionalExists}", null));
                    TreeItems.Clear();
                    TreeItems.Add(errorItem);
                    return;
                }

                System.Diagnostics.Debug.WriteLine("Parsers initialized successfully, loading data...");
                var rootItem = new TreeViewItemViewModel("Coverage Analysis", null);

                // Load coverage data
                System.Diagnostics.Debug.WriteLine("Loading coverage hierarchy...");
                var coverageData = _parserService.GetCoverageHierarchy();
                System.Diagnostics.Debug.WriteLine($"Coverage data result: {coverageData != null}");
                
                if (coverageData != null && coverageData.RootNode != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Coverage data has root node with {coverageData.AllNodes.Count} nodes");
                    var codeItem = new TreeViewItemViewModel("Code", null);
                    var hierarchyItem = CreateHierarchyTreeItem(coverageData.RootNode);
                    codeItem.Children.Add(hierarchyItem);
                    rootItem.Children.Add(codeItem);
                    System.Diagnostics.Debug.WriteLine("Coverage data added to tree");
                }
                else
                {
                    var noCoverageItem = new TreeViewItemViewModel("Code (No data)", null);
                    rootItem.Children.Add(noCoverageItem);
                    System.Diagnostics.Debug.WriteLine("No coverage data available");
                }

                // Load functional data
                System.Diagnostics.Debug.WriteLine("Loading functional coverage...");
                var functionalData = _parserService.GetFunctionalCoverage();
                System.Diagnostics.Debug.WriteLine($"Functional data result: {functionalData != null}");
                
                if (functionalData != null)
                {
                    var functionalItem = new TreeViewItemViewModel("Functional", null);
                    
                    System.Diagnostics.Debug.WriteLine($"Functional groups count: {functionalData.CoverageGroups.Count}");
                    System.Diagnostics.Debug.WriteLine($"Functional points count: {functionalData.CoveragePoints.Count}");
                    
                    if (functionalData.CoverageGroups.Any())
                    {
                        foreach (var group in functionalData.CoverageGroups)
                        {
                            var groupItem = CreateFunctionalGroupTreeItem(group);
                            functionalItem.Children.Add(groupItem);
                        }
                        System.Diagnostics.Debug.WriteLine("Functional groups added to tree");
                    }
                    
                    if (functionalData.CoveragePoints.Any())
                    {
                        foreach (var point in functionalData.CoveragePoints.Take(10))
                        {
                            var pointItem = new TreeViewItemViewModel(point.DisplayText, point);
                            functionalItem.Children.Add(pointItem);
                        }
                        
                        if (functionalData.CoveragePoints.Count > 10)
                        {
                            var moreItem = new TreeViewItemViewModel($"... and {functionalData.CoveragePoints.Count - 10} more", null);
                            functionalItem.Children.Add(moreItem);
                        }
                        System.Diagnostics.Debug.WriteLine("Functional points added to tree");
                    }
                    
                    if (!functionalItem.Children.Any())
                    {
                        functionalItem.Children.Add(new TreeViewItemViewModel("No functional data available", null));
                    }
                    
                    rootItem.Children.Add(functionalItem);
                    System.Diagnostics.Debug.WriteLine("Functional data added to tree");
                }
                else
                {
                    var noFunctionalItem = new TreeViewItemViewModel("Functional (No data)", null);
                    rootItem.Children.Add(noFunctionalItem);
                    System.Diagnostics.Debug.WriteLine("No functional data available");
                }

                System.Diagnostics.Debug.WriteLine("Updating TreeItems...");
                TreeItems.Clear();
                TreeItems.Add(rootItem);
                
                System.Diagnostics.Debug.WriteLine("=== ExecuteLoadData COMPLETED SUCCESSFULLY ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"=== ERROR in ExecuteLoadData: {ex.Message} ===");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                var errorItem = new TreeViewItemViewModel($"Error: {ex.Message}", null);
                errorItem.Children.Add(new TreeViewItemViewModel("Check output window for details", null));
                TreeItems.Clear();
                TreeItems.Add(errorItem);
            }
        }

        private TreeViewItemViewModel CreateHierarchyTreeItem(HierarchyNode node)
        {
            var treeItem = new TreeViewItemViewModel(node.DisplayText, node);
            
            foreach (var child in node.Children)
            {
                var childItem = CreateHierarchyTreeItem(child);
                treeItem.Children.Add(childItem);
            }
            
            return treeItem;
        }

        private TreeViewItemViewModel CreateFunctionalGroupTreeItem(CoverageGroup group)
        {
            var treeItem = new TreeViewItemViewModel(group.DisplayText, group);
            
            foreach (var subGroup in group.SubGroups)
            {
                var subGroupItem = CreateFunctionalGroupTreeItem(subGroup);
                treeItem.Children.Add(subGroupItem);
            }
            
            foreach (var point in group.Points.Take(5)) // Limit for performance
            {
                var pointItem = new TreeViewItemViewModel(point.DisplayText, point);
                treeItem.Children.Add(pointItem);
            }
            
            if (group.Points.Count > 5)
            {
                var moreItem = new TreeViewItemViewModel($"... and {group.Points.Count - 5} more points", null);
                treeItem.Children.Add(moreItem);
            }
            
            return treeItem;
        }

        private void ExecuteRefresh()
        {
            ExecuteLoadData();
        }

        public void Cleanup()
        {
            _parserService.Cleanup();
        }
    }

    public class TreeViewItemViewModel : ViewModelBase
    {
        private bool _isExpanded;
        private bool _isSelected;

        public TreeViewItemViewModel(string displayText, object? tag)
        {
            DisplayText = displayText;
            Tag = tag;
            Children = new ObservableCollection<TreeViewItemViewModel>();
        }

        public string DisplayText { get; set; }
        public object? Tag { get; set; }
        public ObservableCollection<TreeViewItemViewModel> Children { get; set; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public bool HasChildren => Children.Count > 0;
    }
}