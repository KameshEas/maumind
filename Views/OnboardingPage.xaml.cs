using MauMind.App.Services;
using Microsoft.Maui.Controls;

namespace MauMind.App.Views;

public partial class OnboardingPage : ContentPage
{
    private readonly IFirstLaunchService _firstLaunchService;

    // Slide backgrounds: [start color, end color, button text color]
    private static readonly (Color bg, Color btn)[] SlideThemes =
    [
        (Color.FromArgb("#0078D4"), Color.FromArgb("#0078D4")),   // Blue
        (Color.FromArgb("#5B5FC7"), Color.FromArgb("#5B5FC7")),   // Purple
        (Color.FromArgb("#00897B"), Color.FromArgb("#00897B")),   // Teal
        (Color.FromArgb("#1565C0"), Color.FromArgb("#1565C0")),   // Deep Blue
    ];

    // All slides & their XAML named elements
    private VisualElement[] _slides = null!;
    private Frame[] _dots = null!;
    private int _currentIndex = 0;
    private bool _isAnimating = false;

    public OnboardingPage()
    {
        InitializeComponent();

        _firstLaunchService = App.GetService<IFirstLaunchService>();

        _slides = [Slide0, Slide1, Slide2, Slide3];
        _dots   = [Dot0, Dot1, Dot2, Dot3];

        // Set initial background
        BgLayer1.Color = SlideThemes[0].bg;
        BgLayer2.Color = SlideThemes[0].bg;

        Appearing += OnAppearing;
    }

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        // Animate in the first slide
        await AnimateSlideIn(0, isFirst: true);

        // Idle blob floating animation
        _ = FloatBlobsAsync();
    }

    // ─── Navigation ───────────────────────────────────────────────────────────

    private async void OnNextClicked(object sender, EventArgs e)
    {
        if (_isAnimating) return;

        if (_currentIndex < 3)
        {
            await TransitionToSlide(_currentIndex + 1);
        }
        else
        {
            await FinishOnboarding();
        }
    }

    private async void OnSkipClicked(object sender, EventArgs e)
    {
        if (_isAnimating) return;
        await FinishOnboarding();
    }

    private async Task FinishOnboarding()
    {
        _firstLaunchService.CompleteOnboarding();

        // Quick fade-out exit
        await RootGrid.FadeTo(0, 300, Easing.CubicIn);

        // Navigate to shell
        Application.Current!.MainPage = new AppShell();
    }

    // ─── Slide Transitions ────────────────────────────────────────────────────

    private async Task TransitionToSlide(int newIndex)
    {
        _isAnimating = true;

        var outSlide = _slides[_currentIndex];
        var inSlide  = _slides[newIndex];

        // 1. Slide out current (left)
        await outSlide.TranslateTo(-40, 0, 200, Easing.CubicIn);
        var fadeOut = outSlide.FadeTo(0, 180, Easing.CubicIn);
        await fadeOut;
        outSlide.IsVisible = false;
        outSlide.TranslationX = 0;

        // 2. Cross-fade background
        _ = AnimateBackground(newIndex);

        // 3. Animate new slide in
        _currentIndex = newIndex;
        await AnimateSlideIn(newIndex);

        // 4. Update dots
        UpdateDots(newIndex);

        // 5. Update button text
        NextButton.Text = newIndex == 3 ? "Get Started ✦" : "Next →";
        NextButton.TextColor = SlideThemes[newIndex].btn;

        // 6. Hide skip on last slide
        SkipButton.IsVisible = newIndex < 3;

        _isAnimating = false;
    }

    private async Task AnimateSlideIn(int index, bool isFirst = false)
    {
        var slide = _slides[index];

        // Reset position
        slide.TranslationX = isFirst ? 0 : 60;
        slide.Opacity      = 0;
        slide.IsVisible    = true;

        // Translate in + fade
        var slide_in  = slide.TranslateTo(0, 0, 350, Easing.CubicOut);
        var slide_fad = slide.FadeTo(1, 300, Easing.CubicOut);
        await Task.WhenAll(slide_in, slide_fad);

        // Per-slide element animations
        await AnimateSlideElements(index, isFirst);
    }

    private async Task AnimateSlideElements(int index, bool isFirst)
    {
        // Get the icon/title/desc for each slide
        (Frame icon, Label title, Label desc) = index switch
        {
            0 => (S0Icon, S0Title, S0Desc),
            1 => (S1Icon, S1Title, S1Desc),
            2 => (S2Icon, S2Title, S2Desc),
            _ => (S3Icon, S3Title, S3Desc),
        };

        // Reset
        icon.Opacity      = 0;
        icon.Scale        = 0.5;
        icon.TranslationY = isFirst ? -30 : 0;
        title.Opacity     = 0;
        title.TranslationY = 20;
        desc.Opacity      = 0;
        desc.TranslationY = 20;

        // 1. Icon bounces in
        var iconAnim = Task.WhenAll(
            icon.FadeTo(1, 350, Easing.CubicOut),
            icon.ScaleTo(1.15, 250, Easing.CubicOut),
            icon.TranslateTo(0, 0, 300, Easing.CubicOut)
        );
        await iconAnim;
        await icon.ScaleTo(1.0, 150, Easing.BounceOut);

        // 2. Title slides up
        await Task.WhenAll(
            title.FadeTo(1, 280, Easing.CubicOut),
            title.TranslateTo(0, 0, 280, Easing.CubicOut)
        );

        // 3. Description slides up with slight delay
        await Task.Delay(60);
        await Task.WhenAll(
            desc.FadeTo(0.9, 260, Easing.CubicOut),
            desc.TranslateTo(0, 0, 260, Easing.CubicOut)
        );
    }

    // ─── Background Cross-Fade ────────────────────────────────────────────────

    private async Task AnimateBackground(int newIndex)
    {
        BgLayer2.Color   = SlideThemes[newIndex].bg;
        BgLayer2.Opacity = 0;

        await BgLayer2.FadeTo(1, 400, Easing.CubicInOut);

        BgLayer1.Color   = SlideThemes[newIndex].bg;
        BgLayer2.Opacity = 0;
    }

    // ─── Progress Dots ────────────────────────────────────────────────────────

    private void UpdateDots(int activeIndex)
    {
        for (int i = 0; i < _dots.Length; i++)
        {
            bool isActive = i == activeIndex;
            _dots[i].Opacity      = isActive ? 1.0 : 0.4;
            _dots[i].WidthRequest = isActive ? 28 : 10;
        }
    }

    // ─── Floating Blob Animation ──────────────────────────────────────────────

    private async Task FloatBlobsAsync()
    {
        while (true)
        {
            await Task.WhenAll(
                BlobTop.TranslateTo(10, 12, 2500, Easing.SinInOut),
                BlobBottom.TranslateTo(-8, -10, 2500, Easing.SinInOut)
            );
            await Task.WhenAll(
                BlobTop.TranslateTo(0, 0, 2500, Easing.SinInOut),
                BlobBottom.TranslateTo(0, 0, 2500, Easing.SinInOut)
            );
        }
    }
}
