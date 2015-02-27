using System;
using System.Text;

namespace Qiniu.Util
{
    /// <summary>
    /// 字符串处理工具
    /// </summary>
    public class StringUtils
    {
        /// <summary>
        /// 字符串连接
        /// </summary>
        /// <param name="array">字符串数组</param>
        /// <param name="sep">连接符</param>
        /// <returns>连接后字符串</returns>
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

        /// <summary>
        /// 以json格式连接字符串
        /// </summary>
        /// <param name="array">字符串数组</param>
        /// <returns>连接后字符串</returns>
        public static string jsonJoin(string[] array)
        {
            if (array == null)
            {
                return null;
            }
            StringBuilder joined = new StringBuilder();
            int arrayLength = array.Length;
            for (int i = 0; i < arrayLength; i++)
            {
                joined.Append("\"").Append(array[i]).Append("\"");
                if (i < arrayLength - 1)
                {
                    joined.Append(",");
                }
            }
            return joined.ToString();
        }

        /// <summary>
        /// 获取字符串Url安全Base64编码值
        /// </summary>
        /// <param name="from">源字符串</param>
        /// <returns>已编码字符串</returns>
        public static string urlSafeBase64Encode(string from)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(from)).Replace('+', '-').Replace('/', '_');
        }
    }
}
