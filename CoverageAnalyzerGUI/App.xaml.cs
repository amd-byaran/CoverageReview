using System;
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
        //Console.WriteLine("=== WPF APPLICATION STARTUP ===");
        //Console.WriteLine("App.OnStartup called");
        
        // Add global exception handlers
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        
        try
        {
            base.OnStartup(e);
            //Console.WriteLine("App.OnStartup completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in App.OnStartup: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            MessageBox.Show($"Application startup error: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Console.WriteLine($"UNHANDLED EXCEPTION: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            MessageBox.Show($"Unhandled exception: {ex.Message}", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Console.WriteLine($"DISPATCHER UNHANDLED EXCEPTION: {e.Exception.Message}");
        Console.WriteLine($"Stack trace: {e.Exception.StackTrace}");
        MessageBox.Show($"Dispatcher exception: {e.Exception.Message}", "UI Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true; // Prevent app crash
    }
}

