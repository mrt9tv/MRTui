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
/// Similar to TabActiveConverter but compares objects instead of strings
/// </summary>
public class WidgetSelectionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // value = SelectedWidget, parameter = current widget item
        if (value != null && parameter != null && value == parameter)
        {
            return "Active";
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

