using System.Windows;
using MauMind.App.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MauMind.App.Views;

public partial class SplashPage : ContentPage
{
    private readonly IFirstLaunchService _firstLaunchService;
    private bool _isNavigating = false;

    public SplashPage()
    {
        InitializeComponent();
        
        // Get the first launch service
        _firstLaunchService = App.GetService<IFirstLaunchService>();
        
        // Start animations when page appears
        Appearing += OnPageAppearing;
    }

    private async void OnPageAppearing(object? sender, EventArgs e)
    {
        if (_isNavigating) return;
        _isNavigating = true;

        try
        {
            // Start the animation sequence
            await StartAnimationsAsync();
            
            // Wait a bit for user to see the splash
            await Task.Delay(1500);
            
            // Navigate to the appropriate page
            await NavigateToMainAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SplashPage error: {ex.Message}");
            // Still navigate even if animation fails
            await NavigateToMainAsync();
        }
    }

    private async Task StartAnimationsAsync()
    {
        // Step 1: Fade in the logo with scale animation
        LogoImage.Opacity = 0;
        LogoImage.Scale = 0.5;
        
        await Task.WhenAll(
            LogoImage.FadeTo(1, 1000, Easing.CubicOut),
            LogoImage.ScaleTo(1, 1000, Easing.CubicOut)
        );

        // Step 2: Animate the pulsing circle
        await RunPulsingAnimationAsync();

        // Step 3: Fade in app name
        AppNameLabel.Opacity = 0;
        await AppNameLabel.FadeTo(1, 800, Easing.CubicOut);

        // Step 4: Fade in tagline
        TaglineLabel.Opacity = 0;
        await TaglineLabel.FadeTo(1, 800, Easing.CubicOut);

        // Step 5: Fade in loading indicator
        LoadingStack.Opacity = 0;
        await LoadingStack.FadeTo(1, 600, Easing.CubicOut);

        // Step 6: Fade in version
        VersionLabel.Opacity = 0;
        await VersionLabel.FadeTo(0.6, 600, Easing.CubicOut);
    }

    private async Task RunPulsingAnimationAsync()
    {
        // Create a pulsing animation for the circle
        PulsingCircle.Opacity = 0.3;
        
        while (!_isNavigating)
        {
            await Task.WhenAll(
                PulsingCircle.FadeTo(0.6, 1000, Easing.SinInOut),
                PulsingCircle.ScaleTo(1.2, 1000, Easing.SinInOut)
            );
            
            await Task.WhenAll(
                PulsingCircle.FadeTo(0.3, 1000, Easing.SinInOut),
                PulsingCircle.ScaleTo(0.8, 1000, Easing.SinInOut)
            );
        }
    }

    private async Task NavigateToMainAsync()
    {
        // Check if we should show onboarding
        if (_firstLaunchService.IsFirstLaunch && !_firstLaunchService.IsOnboardingComplete)
        {
            // Show onboarding as the new main page inside a NavigationPage
            Application.Current!.MainPage = new NavigationPage(new OnboardingPage());
            await Task.CompletedTask;
        }
        else
        {
            // Navigate to main shell
            Application.Current.MainPage = new AppShell();
        }
    }
}
