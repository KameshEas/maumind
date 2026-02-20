using System.Globalization;
using Microsoft.Maui.Controls;
using MauMind.App.Models;

namespace MauMind.App.Helpers;

public class SuggestionTypeColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SuggestionType type)
        {
            return type switch
            {
                SuggestionType.Grammar => Color.FromArgb("#E53935"), // Red
                SuggestionType.Style => Color.FromArgb("#1E88E5"),   // Blue
                SuggestionType.Clarity => Color.FromArgb("#43A047"), // Green
                SuggestionType.Tone => Color.FromArgb("#FB8C00"),    // Orange
                _ => Color.FromArgb("#757575")                       // Gray
            };
        }
        return Color.FromArgb("#757575");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}