using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace FashionPOS.Converters
{
    /// <summary>
    /// Converts boolean to Visibility (true = Visible, false = Collapsed).
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
                return visibility == Visibility.Visible;
            return false;
        }
    }

    /// <summary>
    /// Converts boolean to Visibility (true = Collapsed, false = Visible).
    /// </summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
                return visibility == Visibility.Collapsed;
            return false;
        }
    }

    /// <summary>
    /// Inverts a boolean value for use with IsEnabled and similar properties.
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return false;
        }
    }

    /// <summary>
    /// Converts string to Visibility (null/empty = Collapsed, else = Visible).
    /// </summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string str)
                return !string.IsNullOrEmpty(str) ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// Converts integer stock quantity to SolidColorBrush (0=Red, <=5=Orange, else=White).
    /// </summary>
    public class StockToColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int stock)
            {
                if (stock <= 0)
                    return new SolidColorBrush(Colors.Red);
                if (stock <= 5)
                    return new SolidColorBrush(Colors.Orange);
            }
            return new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// Converts decimal currency to formatted string.
    /// </summary>
    public class CurrencyConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue || (value is double doubleValue && (decimalValue = (decimal)doubleValue) != 0))
                return decimalValue.ToString("C");
            return "0.00";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string str && decimal.TryParse(str, out var result))
                return result;
            return 0m;
        }
    }

    /// <summary>
    /// Performs a logical AND on multiple boolean values.
    /// </summary>
    public class BooleanAndConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            foreach (var value in values)
            {
                if (value is bool b && !b)
                    return false;
            }
            return true;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Checks if the type of value matches the provided type parameter.
    /// Returns "Active" if matched, null otherwise.
    /// </summary>
    public class TypeMatchConverter : IMultiValueConverter
    {
        public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return null;
            var obj = values[0];
            var type = values[1] as Type;

            if (obj != null && type != null && obj.GetType() == type)
            {
                return "Active";
            }
            return null;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Converts a value into a pixel width for a progress fill based on an expected maximum.
    /// </summary>
    public class ProgressValueToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2)
                return 0.0;

            if (values[1] == DependencyProperty.UnsetValue)
                return 0.0;

            if (!double.TryParse(values[1]?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var trackWidth))
                return 0.0;

            if (!double.TryParse(values[0]?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var currentValue))
                return 0.0;

            if (parameter == null || !double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var maxValue) || maxValue <= 0)
                return 0.0;

            var normalized = Math.Max(0.0, Math.Min(currentValue / maxValue, 1.0));
            var width = trackWidth * normalized;
            return double.IsNaN(width) || double.IsInfinity(width) ? 0.0 : width;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
