using System;
using System.Text;

namespace Qiniu.Util
{
    public class StringUtils
    {
        public static string join(string[] array, string sep)
        {
            if (array == null || sep == null)
            {
                return null;
            }
            StringBuilder joined = new StringBuilder();
            int arrayLength = array.Length;
            for (int i = 0; i < arrayLength; i++)
            {
                joined.Append(array[i]);
                if (i < arrayLength - 1)
                {
                    joined.Append(sep);
                }
            }
            return joined.ToString();
        }

        public static string jsonJoin(string[] array)
        {
            if (array == null)
            {
                return null;
            }
            StringBuilder joined = new StringBuilder();
            int arrayLength = array.Length;
            for(int i=0;i<arrayLength;i++)
            {
                joined.Append("\"").Append(array[i]).Append("\"");
                if(i<arrayLength-1)
                {
                    joined.Append(";");
                }
            }
            return joined.ToString();
        }

        public static string urlSafeBase64Encode(string from)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(from)).Replace('+', '-').Replace('/', '_');
        }
    }
}
