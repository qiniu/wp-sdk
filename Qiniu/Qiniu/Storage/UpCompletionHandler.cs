using Qiniu.Http;

namespace Qiniu.Storage
{
    public delegate void UpCompletionHandler(string key, ResponseInfo info, string response);
}
