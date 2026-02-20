using Microsoft.Maui.Controls;

namespace MauMind.App.Services;

public interface ISkeletonService
{
    View CreateSkeletonLine(double width, double height = 20);
    View CreateSkeletonAvatar(double size = 40);
    View CreateSkeletonCard(double width, double height = 80);
    StackLayout CreateSkeletonList(int items = 5);
    void StartPulseAnimation(View view);
    void StopPulseAnimation(View view);
}

public class SkeletonService : ISkeletonService
{
    private readonly Color _skeletonLight = Color.FromRgb(230, 230, 230);
    private readonly Color _skeletonDark = Color.FromRgb(60, 60, 60);
    
    public View CreateSkeletonLine(double width, double height = 20)
    {
        return new Frame
        {
            WidthRequest = width,
            HeightRequest = height,
            CornerRadius = (float)(height / 2),
            BackgroundColor = _skeletonLight,
            HasShadow = false,
            Margin = new Thickness(0, 2)
        };
    }
    
    public View CreateSkeletonAvatar(double size = 40)
    {
        return new Frame
        {
            WidthRequest = size,
            HeightRequest = size,
            CornerRadius = (float)(size / 2),
            BackgroundColor = _skeletonLight,
            HasShadow = false
        };
    }
    
    public View CreateSkeletonCard(double width, double height = 80)
    {
        return new Frame
        {
            WidthRequest = width,
            HeightRequest = height,
            CornerRadius = 12,
            BackgroundColor = _skeletonLight,
            HasShadow = false,
            Margin = new Thickness(0, 5)
        };
    }
    
    public StackLayout CreateSkeletonList(int items = 5)
    {
        var list = new StackLayout();
        
        for (int i = 0; i < items; i++)
        {
            var card = new Frame
            {
                CornerRadius = 12,
                Padding = 15,
                BackgroundColor = _skeletonLight,
                HasShadow = false,
                Margin = new Thickness(0, 5)
            };
            
            var content = new StackLayout();
            
            // Avatar row
            var row = new StackLayout
            {
                Orientation = StackOrientation.Horizontal
            };
            
            var avatar = CreateSkeletonAvatar(40);
            var textStack = new StackLayout { Margin = new Thickness(10, 0, 0, 0) };
            
            textStack.Children.Add(CreateSkeletonLine(150, 14));
            textStack.Children.Add(CreateSkeletonLine(100, 12));
            
            row.Children.Add(avatar);
            row.Children.Add(textStack);
            
            content.Children.Add(row);
            content.Children.Add(CreateSkeletonLine(250, 12));
            content.Children.Add(CreateSkeletonLine(200, 12));
            
            card.Content = content;
            list.Children.Add(card);
            
            // Start pulse animation
            StartPulseAnimation(card);
        }
        
        return list;
    }
    
    public void StartPulseAnimation(View view)
    {
        var animation = new Animation(v =>
        {
            view.Opacity = v;
        }, 0.4, 1.0, Easing.SinInOut);
        
        animation.Commit(view, "SkeletonPulse", length: 1000, repeat: () => true);
    }
    
    public void StopPulseAnimation(View view)
    {
        view.AbortAnimation("SkeletonPulse");
        view.Opacity = 1.0;
    }
}
