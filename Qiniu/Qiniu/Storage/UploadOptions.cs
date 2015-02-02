using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Qiniu.Storage
{
    public class UploadOptions
    {
        //扩展变量
        public Dictionary<string, string> ExtraParams { set; get; }
        //上传数据或文件的mimeType
        public string MimeType { set; get; }
        //是否对上传文件或数据做crc32校验
        public bool CheckCrc32 { set; get; }
        //上传进度处理
        public UpProgressHandler UpProgressHandler;
        //上传取消信号
        public UpCancellationSignal UpCancellationSignal;
        public UploadOptions(Dictionary<string, string> extraParams, string mimeType,
            bool checkCrc32, UpProgressHandler upProgressHandler, UpCancellationSignal upCancellationSignal)
        {
            this.ExtraParams = filterParams(extraParams);
            this.MimeType = mimeType;
            this.CheckCrc32 = checkCrc32;
            this.UpProgressHandler = upProgressHandler;
            this.UpCancellationSignal = upCancellationSignal;
        }

        //过滤掉所有非x:开头的扩展变量名词
        private Dictionary<string, string> filterParams(Dictionary<string, string> extraParamsToFilter)
        {
             Dictionary<string, string> filtered = new Dictionary<string, string>();
             if (extraParamsToFilter != null)
             {

                 foreach (KeyValuePair<string, string> kvp in extraParamsToFilter)
                 {
                     if (kvp.Key.StartsWith("x:"))
                     {
                         filtered.Add(kvp.Key, kvp.Value);
                     }
                 }
             }
            return filtered;
        }
    }
}