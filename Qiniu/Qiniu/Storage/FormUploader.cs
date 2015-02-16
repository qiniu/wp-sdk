using System.Collections.Generic;
using System.Text;
using System.IO;
using Qiniu.Http;
using Qiniu.Common;
using Qiniu.Util;

namespace Qiniu.Storage
{
    public class FormUploader
    {
        public void uploadData(HttpManager httpManager, byte[] data, string key,
            string token, UploadOptions uploadOptions, CompletionCallback completionCallback)
        {
            PostArgs postArgs = new PostArgs();
            postArgs.Data = data;
            postArgs.Params = new Dictionary<string, string>();
            if (key != null)
            {
                postArgs.FileName = key;
            }
            //set file crc32 check
            if (uploadOptions != null && uploadOptions.CheckCrc32)
            {
                postArgs.Params.Add("crc32", string.Format("{0}", CRC32.CheckSumBytes(data, data.Length)));
            }
            httpManager.FileContentType = PostFileType.BYTES;
            upload(httpManager, postArgs, key, token, uploadOptions, completionCallback);
        }

        public void uploadStream(HttpManager httpManager, Stream stream, string key, string token,
            UploadOptions uploadOptions, CompletionCallback completionCallback)
        {
            PostArgs postArgs = new PostArgs();
            postArgs.Stream = stream;
            postArgs.Params = new Dictionary<string, string>();
            if (key != null)
            {
                postArgs.FileName = key;
            }
            //set file crc32 check
            if (uploadOptions != null && uploadOptions.CheckCrc32)
            {
                long streamLength = stream.Length;
                byte[] buffer = new byte[streamLength];
                int cnt = stream.Read(buffer, 0, (int)streamLength);
                postArgs.Params.Add("crc32", string.Format("{0}", CRC32.CheckSumBytes(buffer, cnt)));
                postArgs.Stream.Seek(0, SeekOrigin.Begin);
            }
            httpManager.FileContentType = PostFileType.STREAM;
            upload(httpManager, postArgs, key, token, uploadOptions, completionCallback);
        }
 
        private void upload(HttpManager httpManager, PostArgs postArgs, string key, string token,
            UploadOptions uploadOptions, CompletionCallback completionCallback)
        {
            //set key
            if (!string.IsNullOrEmpty(key))
            {
                postArgs.Params.Add("key", key);
            }
            //set token
            postArgs.Params.Add("token", token);
            //set mimeType
            string mimeType = "application/octet-stream";
            if (uploadOptions != null && !string.IsNullOrEmpty(uploadOptions.MimeType))
            {
                mimeType = uploadOptions.MimeType;
            }
            postArgs.MimeType = mimeType;
            //set extra params
            if (uploadOptions != null && uploadOptions.ExtraParams != null)
            {
                foreach (KeyValuePair<string, string> kvp in uploadOptions.ExtraParams)
                {
                    postArgs.Params.Add(kvp.Key, kvp.Value);
                }
            }
            //set progress callback and cancellation callback
            if (uploadOptions != null)
            {
                httpManager.ProgressCallback = uploadOptions.ProgressCallback;
                httpManager.CancellationCallback = uploadOptions.CancellationCallback;
            }
            
            httpManager.PostArgs = postArgs;
            //retry once if first time failed
            httpManager.CompletionCallback = new CompletionCallback(delegate(ResponseInfo respInfo,string response){
                if (respInfo.isOk())
                {
                    if (httpManager.PostArgs.Stream != null)
                    {
                        httpManager.PostArgs.Stream.Close();
                    }
                    if (completionCallback != null)
                    {
                        completionCallback(respInfo, response);
                    }
                    return;
                }
                else if(respInfo.needRetry())
                {
                    if (httpManager.PostArgs.Stream != null)
                    {
                        httpManager.PostArgs.Stream.Seek(0, SeekOrigin.Begin);
                    }
                    CompletionCallback retried = new CompletionCallback(delegate(ResponseInfo retryRespInfo,string retryResponse){
                        if (httpManager.PostArgs.Stream != null)
                        {
                            httpManager.PostArgs.Stream.Close();
                        }
                        if (completionCallback != null)
                        {
                            completionCallback(retryRespInfo, retryResponse);
                        }
                    });
                    httpManager.CompletionCallback = retried;
                    httpManager.multipartPost(Config.UP_HOST);
                }
            });
            httpManager.multipartPost(Config.UPLOAD_HOST);
        }
    }
}