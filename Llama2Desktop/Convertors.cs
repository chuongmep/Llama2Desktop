using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Globalization;

namespace WpfChatBot
{
    public class BoolToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string paramString)
            {
                string[] parts = paramString.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int trueValue) && int.TryParse(parts[1], out int falseValue))
                {
                    return boolValue ? trueValue : falseValue;
                }
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string paramString)
            {
                string[] parts = paramString.Split(':');
                if (parts.Length == 2)
                {
                    string colorStr = boolValue ? parts[0] : parts[1];
                    BrushConverter brushConverter = new BrushConverter();
                    return (Brush)brushConverter.ConvertFromString(colorStr);
                }
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToHorizontalAlignmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string paramString)
            {
                string[] parts = paramString.Split(':');
                if (parts.Length == 2)
                {
                    string alignment = boolValue ? parts[0] : parts[1];
                    switch (alignment.ToLower())
                    {
                        case "left":
                            return HorizontalAlignment.Left;
                        case "right":
                            return HorizontalAlignment.Right;
                        case "center":
                            return HorizontalAlignment.Center;
                        case "stretch":
                            return HorizontalAlignment.Stretch;
                    }
                }
            }
            return HorizontalAlignment.Left;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool inverse = parameter as string == "Inverse";
            if (value is bool boolValue)
            {
                if (inverse)
                    boolValue = !boolValue;
                    
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class LengthToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int length)
            {
                return length > 0;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}