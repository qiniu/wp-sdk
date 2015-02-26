using Qiniu.Http;

namespace Qiniu.Storage
{
    //上传完成处理
    public delegate void UpCompletionHandler(string key, ResponseInfo info, string response);
}
