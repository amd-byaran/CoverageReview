using System.Collections.ObjectModel;
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
            var rootItem = new TreeViewItemViewModel("Coverage (Loading...)", null);
            rootItem.Children.Add(new TreeViewItemViewModel("CoverageParser - Not Loaded", null));
            rootItem.Children.Add(new TreeViewItemViewModel("FunctionalParser - Not Loaded", null));
            
            TreeItems.Clear();
            TreeItems.Add(rootItem);
        }

        private void ExecuteLoadData()
        {
            // Initialize parsers with actual data directories
            string coverageDataPath = @"C:\Users\byaran\OneDrive - Advanced Micro Devices Inc\programming\coverage_analyzer\CoverageParser\data\code";
            string functionalDataPath = @"C:\Users\byaran\OneDrive - Advanced Micro Devices Inc\programming\coverage_analyzer\FunctionalParser\functional";

            if (_parserService.InitializeParsers(coverageDataPath, functionalDataPath))
            {
                LoadCoverageData();
            }
            else
            {
                // Show error or fallback
                var errorItem = new TreeViewItemViewModel("Coverage (Error Loading Data)", null);
                errorItem.Children.Add(new TreeViewItemViewModel("Failed to initialize parsers", null));
                TreeItems.Clear();
                TreeItems.Add(errorItem);
            }
        }

        private void LoadCoverageData()
        {
            var rootItem = new TreeViewItemViewModel("Coverage", null);

            // Load CoverageParser data
            var coverageData = _parserService.GetCoverageHierarchy();
            if (coverageData?.RootNode != null)
            {
                var coverageParserItem = new TreeViewItemViewModel("CoverageParser", null);
                var hierarchyItem = CreateHierarchyTreeItem(coverageData.RootNode);
                coverageParserItem.Children.Add(hierarchyItem);
                rootItem.Children.Add(coverageParserItem);
            }

            // Load FunctionalParser data
            var functionalData = _parserService.GetFunctionalCoverage();
            if (functionalData != null)
            {
                var functionalParserItem = new TreeViewItemViewModel("FunctionalParser", null);
                
                foreach (var group in functionalData.CoverageGroups)
                {
                    var groupItem = CreateFunctionalGroupTreeItem(group);
                    functionalParserItem.Children.Add(groupItem);
                }
                
                rootItem.Children.Add(functionalParserItem);
            }

            TreeItems.Clear();
            TreeItems.Add(rootItem);
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
            
            foreach (var point in group.Points.Take(10)) // Limit to first 10 points for UI performance
            {
                var pointItem = new TreeViewItemViewModel(point.DisplayText, point);
                treeItem.Children.Add(pointItem);
            }
            
            if (group.Points.Count > 10)
            {
                var moreItem = new TreeViewItemViewModel($"... and {group.Points.Count - 10} more points", null);
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
        private string _header;
        private bool _isExpanded;
        private bool _isSelected;
        private object? _tag;

        public TreeViewItemViewModel(string header, object? tag)
        {
            _header = header;
            _tag = tag;
            Children = new ObservableCollection<TreeViewItemViewModel>();
        }

        public string Header
        {
            get => _header;
            set => SetProperty(ref _header, value);
        }

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

        public object? Tag
        {
            get => _tag;
            set => SetProperty(ref _tag, value);
        }

        public ObservableCollection<TreeViewItemViewModel> Children { get; }
    }
}