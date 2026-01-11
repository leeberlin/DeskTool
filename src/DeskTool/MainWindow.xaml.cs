using DeskTool.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Serilog;
using System;
using Windows.System;

namespace DeskTool;

/// <summary>
/// Main application window with navigation between Image OCR and PDF Tools modules.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        Title = "DeskTool - OCR & PDF Tools";
        ExtendsContentIntoTitleBar = false;
        
        // Set up keyboard shortcuts
        Content.KeyDown += Content_KeyDown;
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        // Navigate to Image OCR by default
        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(ImageOcrPage));
        
        Log.Debug("Navigation loaded, defaulted to ImageOcrPage");
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            Type? pageType = tag switch
            {
                "ImageOcr" => typeof(ImageOcrPage),
                "PdfTools" => typeof(PdfToolsPage),
                _ => null
            };

            if (pageType != null && ContentFrame.CurrentSourcePageType != pageType)
            {
                ContentFrame.Navigate(pageType);
                Log.Debug("Navigated to {PageType}", pageType.Name);
            }
        }
    }

    private void Content_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Global hotkeys
        var ctrlPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (ctrlPressed)
        {
            switch (e.Key)
            {
                case VirtualKey.O:
                    // Trigger open file in current page
                    if (ContentFrame.Content is IFileOpenHandler handler)
                    {
                        handler.OpenFileAsync();
                        e.Handled = true;
                    }
                    break;
                    
                case VirtualKey.Number1:
                    // Switch to Image OCR
                    NavView.SelectedItem = NavView.MenuItems[0];
                    e.Handled = true;
                    break;
                    
                case VirtualKey.Number2:
                    // Switch to PDF Tools
                    NavView.SelectedItem = NavView.MenuItems[1];
                    e.Handled = true;
                    break;
            }
        }
    }
}

/// <summary>
/// Interface for pages that support file opening via hotkey.
/// </summary>
public interface IFileOpenHandler
{
    void OpenFileAsync();
}
