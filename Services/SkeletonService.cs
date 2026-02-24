using Microsoft.Maui.Controls;

namespace MauMind.App.Services;

/// <summary>
/// Premium Skeleton Loading System - Linear/Vercel Style
/// Provides shimmer and pulse animations for loading states
/// </summary>
public interface ISkeletonService
{
    Frame CreateSkeletonLine(double width, double height = 16);
    Frame CreateSkeletonAvatar(double size = 40);
    Frame CreateSkeletonCard(double width, double height = 80);
    Frame CreateSkeletonText(int lines = 2);
    StackLayout CreateSkeletonList(int items = 5);
    StackLayout CreateSkeletonChatMessages(int messages = 3);
    void StartShimmerAnimation(Frame frame);
    void StopShimmerAnimation(Frame frame);
    void ApplyThemeColors(Frame frame, bool isDarkMode);
}

public class SkeletonService : ISkeletonService
{
    private readonly Color _skeletonLight = Color.FromRgb(235, 235, 235);
    private readonly Color _skeletonDark = Color.FromRgb(50, 50, 52);
    
    private readonly Color _skeletonHighlightLight = Color.FromRgb(250, 250, 250);
    private readonly Color _skeletonHighlightDark = Color.FromRgb(65, 65, 67);

    /// <summary>
    /// Creates a skeleton line (like Linear)
    /// </summary>
    public Frame CreateSkeletonLine(double width, double height = 16)
    {
        var frame = new Frame
        {
            WidthRequest = width,
            HeightRequest = height,
            CornerRadius = (float)(height / 2),
            BackgroundColor = _skeletonLight,
            HasShadow = false,
            Margin = new Thickness(0, 2),
            Padding = 0
        };
        
        StartShimmerAnimation(frame);
        return frame;
    }

    /// <summary>
    /// Creates a skeleton avatar circle
    /// </summary>
    public Frame CreateSkeletonAvatar(double size = 40)
    {
        var frame = new Frame
        {
            WidthRequest = size,
            HeightRequest = size,
            CornerRadius = (float)(size / 2),
            BackgroundColor = _skeletonLight,
            HasShadow = false,
            Padding = 0
        };
        
        StartShimmerAnimation(frame);
        return frame;
    }

    /// <summary>
    /// Creates a skeleton card/box
    /// </summary>
    public Frame CreateSkeletonCard(double width, double height = 80)
    {
        var frame = new Frame
        {
            WidthRequest = width,
            HeightRequest = height,
            CornerRadius = 12,
            BackgroundColor = _skeletonLight,
            HasShadow = false,
            Margin = new Thickness(0, 4),
            Padding = 0
        };
        
        StartShimmerAnimation(frame);
        return frame;
    }

    /// <summary>
    /// Creates multi-line skeleton text
    /// </summary>
    public Frame CreateSkeletonText(int lines = 2)
    {
        var stack = new StackLayout { Spacing = 6 };
        
        // First line (full width)
        stack.Children.Add(CreateSkeletonLine(200, 14));
        
        // Additional lines (varying widths)
        for (int i = 1; i < lines; i++)
        {
            double width = 200 - (i * 30);
            width = Math.Max(width, 100);
            stack.Children.Add(CreateSkeletonLine(width, 12));
        }
        
        var frame = new Frame
        {
            CornerRadius = 8,
            BackgroundColor = _skeletonLight,
            HasShadow = false,
            Padding = 12,
            Content = stack,
            Margin = new Thickness(0, 4)
        };
        
        return frame;
    }

    /// <summary>
    /// Creates a skeleton list (like document/chat lists)
    /// </summary>
    public StackLayout CreateSkeletonList(int items = 5)
    {
        var list = new StackLayout { Spacing = 12 };
        
        for (int i = 0; i < items; i++)
        {
            var card = CreateSkeletonListItem();
            list.Children.Add(card);
        }
        
        return list;
    }

    /// <summary>
    /// Creates a single skeleton list item
    /// </summary>
    private Frame CreateSkeletonListItem()
    {
        var card = new Frame
        {
            CornerRadius = 14,
            Padding = 14,
            BackgroundColor = _skeletonLight,
            HasShadow = false,
            Margin = new Thickness(0, 0)
        };
        
        var content = new StackLayout { Spacing = 10 };
        
        // Icon/Avatar row
        var row = new StackLayout { Orientation = StackOrientation.Horizontal };
        
        var avatar = CreateSkeletonAvatar(40);
        var textStack = new StackLayout { Margin = new Thickness(12, 0, 0, 0), Spacing = 6 };
        
        textStack.Children.Add(CreateSkeletonLine(150, 15));
        textStack.Children.Add(CreateSkeletonLine(100, 12));
        
        row.Children.Add(avatar);
        row.Children.Add(textStack);
        
        content.Children.Add(row);
        
        // Description lines
        content.Children.Add(CreateSkeletonLine(250, 12));
        content.Children.Add(CreateSkeletonLine(180, 12));
        
        card.Content = content;
        
        StartShimmerAnimation(card);
        return card;
    }

    /// <summary>
    /// Creates skeleton chat messages
    /// </summary>
    public StackLayout CreateSkeletonChatMessages(int messages = 3)
    {
        var list = new StackLayout { Spacing = 16 };
        
        for (int i = 0; i < messages; i++)
        {
            bool isUser = i % 2 == 1;
            var message = CreateSkeletonChatMessage(isUser);
            list.Children.Add(message);
        }
        
        return list;
    }

    /// <summary>
    /// Creates a single skeleton chat message
    /// </summary>
    private Frame CreateSkeletonChatMessage(bool isUser)
    {
        double width = isUser ? 220 : 280;
        
        var frame = new Frame
        {
            CornerRadius = 16,
            Padding = 14,
            BackgroundColor = _skeletonLight,
            HasShadow = false,
            HorizontalOptions = isUser ? LayoutOptions.End : LayoutOptions.Start,
            MaximumWidthRequest = width
        };
        
        var stack = new StackLayout { Spacing = 8 };
        
        // Avatar (for AI messages)
        if (!isUser)
        {
            var headerRow = new StackLayout { Orientation = StackOrientation.Horizontal, Spacing = 10 };
            headerRow.Children.Add(CreateSkeletonAvatar(28));
            headerRow.Children.Add(CreateSkeletonLine(80, 12));
            stack.Children.Add(headerRow);
        }
        
        // Message lines
        stack.Children.Add(CreateSkeletonLine(width - (isUser ? 0 : 40), 14));
        stack.Children.Add(CreateSkeletonLine(width - 60, 12));
        
        frame.Content = stack;
        
        StartShimmerAnimation(frame);
        return frame;
    }

    /// <summary>
    /// Starts shimmer/pulse animation (Linear-style subtle pulse)
    /// </summary>
    public void StartShimmerAnimation(Frame frame)
    {
        // Subtle opacity animation (Linear style)
        var animation = new Animation(v =>
        {
            frame.Opacity = v;
        }, 0.5, 1.0, Easing.SinInOut);
        
        animation.Commit(frame, "SkeletonShimmer", length: 1200, repeat: () => true);
    }

    /// <summary>
    /// Stops shimmer animation
    /// </summary>
    public void StopShimmerAnimation(Frame frame)
    {
        frame.AbortAnimation("SkeletonShimmer");
        frame.Opacity = 1.0;
    }

    /// <summary>
    /// Applies theme colors based on dark/light mode
    /// </summary>
    public void ApplyThemeColors(Frame frame, bool isDarkMode)
    {
        frame.BackgroundColor = isDarkMode ? _skeletonDark : _skeletonLight;
    }
}
