using System.Collections.Generic;
using System.Diagnostics;

namespace Qiniu.Storage
{
    public class UploadOptions
    {
        //扩展变量,名词必须以x:开头，另外值不能为空
        public Dictionary<string, string> ExtraParams { set; get; }
        //上传数据或文件的mimeType
        public string MimeType { set; get; }
        //是否对上传文件或数据做crc32校验
        public bool CheckCrc32 { set; get; }
        //上传进度处理器
        public UpProgressHandler ProgressHandler { set; get; }
        //上传取消信号
        public UpCancellationSignal CancellationSignal { set; get; }

        public UploadOptions(Dictionary<string, string> extraParams, string mimeType, bool checkCrc32,
            UpProgressHandler upProgressHandler, UpCancellationSignal upCancellationSignal)
        {
            this.ExtraParams = filterParams(extraParams);
            this.MimeType = mime(mimeType);
            this.CheckCrc32 = checkCrc32;
            this.CancellationSignal = (upCancellationSignal != null) ? upCancellationSignal : new UpCancellationSignal(delegate()
            {
                return false;
            });
            this.ProgressHandler = (upProgressHandler != null) ? upProgressHandler : new UpProgressHandler(delegate(string key, double percent)
            {
                Debug.WriteLine("qiniu up progress " + percent + "%");
            });
        }

        public static UploadOptions defaultOptions()
        {
            return new UploadOptions(null, null, false, null, null);
        }

        //过滤掉所有非x:开头的或者值为空的扩展变量
        private Dictionary<string, string> filterParams(Dictionary<string, string> extraParamsToFilter)
        {
            Dictionary<string, string> filtered = new Dictionary<string, string>();
            if (extraParamsToFilter != null)
            {

                foreach (KeyValuePair<string, string> kvp in extraParamsToFilter)
                {
                    if (kvp.Key.StartsWith("x:") && kvp.Value != null && kvp.Value.Trim().Length > 0)
                    {
                        filtered.Add(kvp.Key, kvp.Value);
                    }
                }
            }
            return filtered;
        }

        //
        private string mime(string mimeType)
        {
            if (mimeType == null || mimeType.Trim().Length == 0)
            {
                return "application/octet-stream";
            }
            return mimeType;
        }
    }
}