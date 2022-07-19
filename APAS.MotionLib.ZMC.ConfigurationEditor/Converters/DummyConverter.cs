﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;

namespace APAS.MotionLib.ZMC.ConfigurationEditor.Converters
{
    internal class DummyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Debug.WriteLine(value?.GetType().Name);
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Debug.WriteLine(value?.GetType().Name);
            return value;
        }
    }
}