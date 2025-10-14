using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace iRacingOverlay.WPF.Converters;

/// <summary>
/// Converts tab name to Visibility (Visible if matches parameter, Collapsed otherwise)
/// </summary>
public class TabVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string selectedTab && parameter is string tabName)
        {
            return selectedTab == tabName ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts tab name to "Active" tag (for button styling)
/// </summary>
public class TabActiveConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string selectedTab && parameter is string tabName)
        {
            return selectedTab == tabName ? "Active" : string.Empty;
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Inverts a boolean value
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return true;
    }
}

/// <summary>
/// Converts widget item to "Active" tag (for button styling in Overlay Manager)
/// Uses MultiValueConverter to compare SelectedWidget with current widget item
/// </summary>
public class WidgetSelectionConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // values[0] = SelectedWidget from DataContext
        // values[1] = Current widget item (this button's DataContext)
        if (values.Length == 2 && values[0] != null && values[1] != null && values[0] == values[1])
        {
            return "Active";
        }
        return string.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

