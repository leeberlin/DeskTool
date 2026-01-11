using DeskTool.Core.Services;
using DeskTool.ViewModels;
using DeskTool.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Serilog;
using System;
using System.IO;

namespace DeskTool;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    
    /// <summary>
    /// Gets the service provider for dependency injection.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;
    
    /// <summary>
    /// Gets the current App instance.
    /// </summary>
    public static new App Current => (App)Application.Current;

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        InitializeComponent();
        ConfigureLogging();
        ConfigureServices();
        
        UnhandledException += App_UnhandledException;
    }

    private void ConfigureLogging()
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeskTool", "Logs", "desktool-.log");
        
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(logPath, 
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Console()
            .CreateLogger();
        
        Log.Information("DeskTool starting up");
    }

    private void ConfigureServices()
    {
        var services = new ServiceCollection();
        
        // Core services
        services.AddSingleton<IOcrService, TesseractOcrService>();
        services.AddSingleton<IPdfService, PdfSharpService>();
        services.AddSingleton<IImageProcessingService, ImageSharpProcessingService>();
        services.AddSingleton<IFileService, FileService>();
        
        // ViewModels
        services.AddTransient<ImageOcrViewModel>();
        services.AddTransient<PdfToolsViewModel>();
        services.AddTransient<MainViewModel>();
        
        Services = services.BuildServiceProvider();
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled exception occurred");
        e.Handled = true;
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
        
        Log.Information("MainWindow activated");
    }
}
