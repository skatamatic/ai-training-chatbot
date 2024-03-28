using System;
using System.Globalization;
using System.Windows.Data;

namespace AI_Training_API;

public class BoolToHeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isChecked = (bool)value;
        int linesToShow = 2;
        // Assuming 18 is the font size and 2 is the line height factor
        return isChecked ? double.PositiveInfinity : linesToShow * 22 * 2;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
