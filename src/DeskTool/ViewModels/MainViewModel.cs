using CommunityToolkit.Mvvm.ComponentModel;

namespace DeskTool.ViewModels;

/// <summary>
/// Main ViewModel for the application.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _currentPage = "ImageOcr";

    [ObservableProperty]
    private bool _isDarkMode = true;

    public void NavigateTo(string pageName)
    {
        CurrentPage = pageName;
    }
}
