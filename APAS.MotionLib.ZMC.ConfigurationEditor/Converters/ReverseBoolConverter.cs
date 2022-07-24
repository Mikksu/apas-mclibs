using System;
using System.Globalization;
using System.Windows.Data;

namespace APAS.MotionLib.ZMC.ConfigurationEditor.Converters
{
    internal class ReverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            else
                return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
