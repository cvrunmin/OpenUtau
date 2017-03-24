using OpenUtau.Core.USTx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
}
