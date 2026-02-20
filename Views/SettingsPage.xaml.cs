using MauMind.App.ViewModels;

namespace MauMind.App.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;
    
    public SettingsPage()
    {
        InitializeComponent();
        
        _viewModel = App.GetService<SettingsViewModel>();
        BindingContext = _viewModel;
        
        Loaded += (s, e) => _viewModel.Initialize();
    }
}
