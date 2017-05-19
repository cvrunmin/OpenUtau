using OpenUtau.Core.USTx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Controls;

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

    public static class ColorsConverter
    {
        public static Color ToMediaColor(this System.Drawing.Color drawingColor) => 
            new Color() { R = drawingColor.R, G = drawingColor.G, B = drawingColor.B, A = drawingColor.A };

        public static System.Drawing.Color ToDrawingColor(this Color mediaColor) =>
            System.Drawing.Color.FromArgb(red: mediaColor.R, green: mediaColor.G, blue: mediaColor.B, alpha: mediaColor.A);
    }

    public class NumericValidationRule : ValidationRule
    {
        public Type ValidationType { get; set; }
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            string strValue = Convert.ToString(value);

            if (string.IsNullOrEmpty(strValue))
                return new ValidationResult(false, $"Value cannot be coverted to string.");
            bool canConvert = false;
            switch (ValidationType.Name)
            {

                case "Boolean":
                    bool boolVal = false;
                    canConvert = bool.TryParse(strValue, out boolVal);
                    return canConvert ? new ValidationResult(true, null) : new ValidationResult(false, $"Input should be type of boolean");
                case "Int32":
                    int intVal = 0;
                    canConvert = int.TryParse(strValue, out intVal);
                    return canConvert ? new ValidationResult(true, null) : new ValidationResult(false, $"Input should be type of Int32");
                case "Double":
                    double doubleVal = 0;
                    canConvert = double.TryParse(strValue, out doubleVal);
                    return canConvert ? new ValidationResult(true, null) : new ValidationResult(false, $"Input should be type of Double");
                case "Int64":
                    long longVal = 0;
                    canConvert = long.TryParse(strValue, out longVal);
                    return canConvert ? new ValidationResult(true, null) : new ValidationResult(false, $"Input should be type of Int64");
                default:
                    throw new InvalidCastException($"{ValidationType.Name} is not supported");
            }
        }
    }
}
