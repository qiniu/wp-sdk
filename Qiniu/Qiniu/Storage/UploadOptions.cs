using Qiniu.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public ProgressCallback ProgressCallback { set; get; }
        //上传取消信号
        public CancellationCallback CancellationCallback { set; get; }
        public UploadOptions()
        {
            this.ExtraParams = null;
            this.MimeType = null;
            this.CheckCrc32 = false;
            this.CancellationCallback = null;
            this.ProgressCallback = null;
        }

        public UploadOptions(Dictionary<string, string> extraParams, string mimeType, bool checkCrc32,
            ProgressCallback progressCallback, CancellationCallback cancellationCallback)
        {
            this.ExtraParams = filterParams(extraParams);
            this.MimeType = mimeType;
            this.CheckCrc32 = checkCrc32;
            this.CancellationCallback = cancellationCallback;
            this.ProgressCallback = progressCallback;
        }

        //过滤掉所有非x:开头的或者值为空的扩展变量
        private Dictionary<string, string> filterParams(Dictionary<string, string> extraParamsToFilter)
        {
            Dictionary<string, string> filtered = new Dictionary<string, string>();
            if (extraParamsToFilter != null)
            {

                foreach (KeyValuePair<string, string> kvp in extraParamsToFilter)
                {
                    if (kvp.Key.StartsWith("x:") && kvp.Value.Trim().Length > 0)
                    {
                        filtered.Add(kvp.Key, kvp.Value);
                    }
                }
            }
            return filtered;
        }
    }
}