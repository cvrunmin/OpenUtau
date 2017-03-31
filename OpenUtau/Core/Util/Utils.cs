using OpenUtau.Core.USTx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Globalization;
using System.Windows.Media;

namespace OpenUtau.Core.Util
{
    class Utils
    {
        public static void SetExpresstionValue(UExpression expression, object obj)
        {
            if (expression is IntExpression)
            {
                expression.Data = (int)obj;
            }
            if (expression is FloatExpression)
            {
                expression.Data = (float)obj;
            }
        }
        public static void SetExpresstionValue(UExpression expression, string obj)
        {
            if (expression is IntExpression)
            {
                expression.Data = int.Parse(obj);
            }
            if (expression is FloatExpression)
            {
                expression.Data = float.Parse(obj);
            }
        }
    }

    class RegionWidthConverter : IMultiValueConverter
    {

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var doubleValues = values.Cast<double>().ToArray(); //expected: 0: value, 1:maximum value, 2:actual width
                return doubleValues[0] / doubleValues[1] * doubleValues[2];
            }
            catch (Exception)
            {
                return 0d;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
