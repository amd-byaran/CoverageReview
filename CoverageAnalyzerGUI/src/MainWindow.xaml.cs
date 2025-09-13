using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using CoverageAnalyzerGUI.ViewModels;

namespace CoverageAnalyzerGUI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        
        // Bind ViewModel properties to UI elements
        var viewModel = (MainWindowViewModel)DataContext;
        
        // Bind StatusText to StatusBar
        var statusBinding = new Binding("StatusText") { Source = viewModel };
        StatusText.SetBinding(TextBlock.TextProperty, statusBinding);
        
        // Bind ParserStatus to StatusBar
        var parserStatusBinding = new Binding("ParserStatus") { Source = viewModel };
        ParserStatus.SetBinding(TextBlock.TextProperty, parserStatusBinding);
        
        // Bind FileCount to StatusBar
        var fileCountBinding = new Binding("FileCount") { Source = viewModel };
        FileCount.SetBinding(TextBlock.TextProperty, fileCountBinding);
        
        // Bind OutputText to OutputTextBox
        var outputBinding = new Binding("OutputText") { Source = viewModel };
        OutputTextBox.SetBinding(TextBox.TextProperty, outputBinding);
        
        // Auto-scroll to bottom when new output is added
        OutputTextBox.TextChanged += (s, e) =>
        {
            OutputTextBox.ScrollToEnd();
        };
    }
}