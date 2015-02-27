using System.Collections.Generic;
using System.IO;

namespace Qiniu.Http
{
    /// <summary>
    /// 定义multipart/form-data的请求参数
    /// 其中Data, Stream, File只能设置一个
    /// </summary>
    public class PostArgs
    {
        //上传的数据
        public byte[] Data { set; get; }
        // 上传的文件流
        public Stream Stream { set; get; }
        // 上传的文件，只能是IsolatedStorage中的文件
        public string File { set; get; }
        //请求参数
        public Dictionary<string, string> Params { set; get; }
        //上传数据或文件的原始名称
        public string FileName { set; get; }
        //上传数据或文件的类型
        public string MimeType { set; get; }

        public PostArgs()
        {
            this.Params = new Dictionary<string, string>();
            this.Data = null;
            this.Stream = null;
            this.FileName = null;
            this.MimeType = null;
        }
    }
}
