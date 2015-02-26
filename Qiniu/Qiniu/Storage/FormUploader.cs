using System.Collections.Generic;
using System.IO;
using Qiniu.Http;
using Qiniu.Common;
using Qiniu.Util;

namespace Qiniu.Storage
{
    public class FormUploader
    {
        public void uploadData(HttpManager httpManager, byte[] data, string key,
            string token, UploadOptions uploadOptions, UpCompletionHandler upCompletionHandler)
        {
            PostArgs postArgs = new PostArgs();
            postArgs.Data = data;
            if (key != null)
            {
                postArgs.FileName = key;
            }
            httpManager.FileContentType = PostContentType.BYTES;
            upload(httpManager, postArgs, key, token, uploadOptions, upCompletionHandler);
        }

        public void uploadStream(HttpManager httpManager, Stream stream, string key, string token,
            UploadOptions uploadOptions, UpCompletionHandler upCompletionHandler)
        {
            PostArgs postArgs = new PostArgs();
            postArgs.Stream = stream;
            if (key != null)
            {
                postArgs.FileName = key;
            }
            httpManager.FileContentType = PostContentType.STREAM;
            upload(httpManager, postArgs, key, token, uploadOptions, upCompletionHandler);
        }

        public void uploadFile(HttpManager httpManager, string filePath, string key,
            string token, UploadOptions uploadOptions, UpCompletionHandler upCompletionHandler)
        {
            PostArgs postArgs = new PostArgs();
            postArgs.File = filePath;
            postArgs.FileName = Path.GetFileName(filePath);
            httpManager.FileContentType = PostContentType.FILE;
            upload(httpManager, postArgs, key, token, uploadOptions, upCompletionHandler);
        }

        private void upload(HttpManager httpManager, PostArgs postArgs, string key, string token,
            UploadOptions uploadOptions, UpCompletionHandler upCompletionHandler)
        {
            if (uploadOptions == null)
            {
                uploadOptions = UploadOptions.defaultOptions();
            }
            postArgs.Params = new Dictionary<string, string>();
            //set key
            if (!string.IsNullOrEmpty(key))
            {
                postArgs.Params.Add("key", key);
            }
            //set token
            postArgs.Params.Add("token", token);
            //set check crc32
            if (uploadOptions.CheckCrc32)
            {
                switch (httpManager.FileContentType)
                {
                    case PostContentType.BYTES:
                        postArgs.Params.Add("crc32", string.Format("{0}", CRC32.CheckSumBytes(postArgs.Data, postArgs.Data.Length)));
                        break;
                    case PostContentType.STREAM:
                        long streamLength = postArgs.Stream.Length;
                        byte[] buffer = new byte[streamLength];
                        int cnt = postArgs.Stream.Read(buffer, 0, (int)streamLength);
                        postArgs.Params.Add("crc32", string.Format("{0}", CRC32.CheckSumBytes(buffer, cnt)));
                        postArgs.Stream.Seek(0, SeekOrigin.Begin);
                        break;
                    case PostContentType.FILE:
                        postArgs.Params.Add("crc32", string.Format("{0}", CRC32.CheckSumFile(postArgs.File)));
                        break;
                }
            }

            //set mimeType
            postArgs.MimeType = uploadOptions.MimeType;
            //set extra params
            foreach (KeyValuePair<string, string> kvp in uploadOptions.ExtraParams)
            {
                postArgs.Params.Add(kvp.Key, kvp.Value);
            }

            //set progress callback and cancellation callback
            httpManager.ProgressHandler = new ProgressHandler(delegate(int bytesWritten, int totalBytes)
            {
                double percent = (double)bytesWritten / totalBytes;
                uploadOptions.ProgressHandler(key, percent);
            });

            httpManager.CancellationSignal = new CancellationSignal(delegate()
            {
                return uploadOptions.CancellationSignal();
            });
            httpManager.PostArgs = postArgs;
            //retry once if first time failed
            httpManager.CompletionHandler = new CompletionHandler(delegate(ResponseInfo respInfo, string response)
            {
                if (respInfo.needRetry())
                {
                    if (httpManager.PostArgs.Stream != null)
                    {
                        httpManager.PostArgs.Stream.Seek(0, SeekOrigin.Begin);
                    }
                    CompletionHandler retried = new CompletionHandler(delegate(ResponseInfo retryRespInfo, string retryResponse)
                    {
                        if (httpManager.PostArgs.Stream != null)
                        {
                            httpManager.PostArgs.Stream.Close();
                        }
                        if (upCompletionHandler != null)
                        {
                            upCompletionHandler(key, retryRespInfo, retryResponse);
                        }
                    });
                    httpManager.CompletionHandler = retried;
                    httpManager.multipartPost(Config.UP_HOST);
                }
                else
                {
                    if (httpManager.PostArgs.Stream != null)
                    {
                        httpManager.PostArgs.Stream.Close();
                    }
                    if (upCompletionHandler != null)
                    {
                        upCompletionHandler(key, respInfo, response);
                    }
                }
            });
            httpManager.multipartPost(Config.UPLOAD_HOST);
        }
    }
}