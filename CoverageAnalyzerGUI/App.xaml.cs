using System.Configuration;
using System.Data;
using System.Windows;

namespace CoverageAnalyzerGUI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        Console.WriteLine("=== WPF APPLICATION STARTUP ===");
        Console.WriteLine("App.OnStartup called");
        base.OnStartup(e);
        Console.WriteLine("App.OnStartup completed");
    }
}

