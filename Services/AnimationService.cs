using Microsoft.Maui.Controls;

namespace MauMind.App.Services;

public interface IAnimationService
{
    Task FadeInAsync(View view, uint duration = 300);
    Task FadeOutAsync(View view, uint duration = 300);
    Task SlideInFromRightAsync(View view, uint duration = 300);
    Task SlideInFromBottomAsync(View view, uint duration = 300);
    Task ScaleAsync(View view, double scale, uint duration = 200);
    Task PulseAsync(View view, uint duration = 500);
    void AddPressAnimation(Button button);
    void AddLoadingAnimation(View view);
}

public class AnimationService : IAnimationService
{
    public async Task FadeInAsync(View view, uint duration = 300)
    {
        view.Opacity = 0;
        await view.FadeTo(1, duration, Easing.CubicOut);
    }
    
    public async Task FadeOutAsync(View view, uint duration = 300)
    {
        await view.FadeTo(0, duration, Easing.CubicIn);
    }
    
    public async Task SlideInFromRightAsync(View view, uint duration = 300)
    {
        var translationX = view.TranslationX;
        view.TranslationX = 100;
        view.Opacity = 0;
        
        await Task.WhenAll(
            view.TranslateTo(translationX, 0, duration, Easing.CubicOut),
            view.FadeTo(1, duration, Easing.CubicOut)
        );
    }
    
    public async Task SlideInFromBottomAsync(View view, uint duration = 300)
    {
        var translationY = view.TranslationY;
        view.TranslationY = 50;
        view.Opacity = 0;
        
        await Task.WhenAll(
            view.TranslateTo(0, translationY, duration, Easing.CubicOut),
            view.FadeTo(1, duration, Easing.CubicOut)
        );
    }
    
    public async Task ScaleAsync(View view, double scale, uint duration = 200)
    {
        await view.ScaleTo(scale, duration, Easing.CubicOut);
    }
    
    public async Task PulseAsync(View view, uint duration = 500)
    {
        await view.ScaleTo(1.1, duration / 2, Easing.CubicOut);
        await view.ScaleTo(1.0, duration / 2, Easing.CubicIn);
    }
    
    public void AddPressAnimation(Button button)
    {
        button.Pressed += async (s, e) =>
        {
            await button.ScaleTo(0.95, 100, Easing.CubicOut);
        };
        
        button.Released += async (s, e) =>
        {
            await button.ScaleTo(1.0, 100, Easing.CubicOut);
        };
    }
    
    public void AddLoadingAnimation(View view)
    {
        // Create a continuous pulse animation
        var animation = new Animation(v =>
        {
            view.Opacity = v;
        }, 0.5, 1.0, Easing.SinInOut);
        
        animation.Commit(view, "LoadingPulse", length: 1000, repeat: () => true);
    }
}
