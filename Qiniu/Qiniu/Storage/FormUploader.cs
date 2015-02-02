using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using Qiniu.Http;
using DBTek.Crypto;
using Qiniu.Common;

namespace Qiniu.Storage
{
    public class FormUploader
    {
        public static void uploadData(HttpManager httpManager, byte[] data, string key,
            string token, UploadOptions uploadOptions, UpCompletionHandler upCompletionHandler)
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
                postArgs.Params.Add("crc32", new CRC32_Hsr().HashString(Encoding.UTF8.GetString(data, 0, data.Length)));
            }
            httpManager.UseData = true;
            upload(httpManager, postArgs, key, token, uploadOptions, upCompletionHandler);
        }

        public static void uploadStream(HttpManager httpManager, Stream stream, string key, string token,
            UploadOptions uploadOptions, UpCompletionHandler upCompletionHandler)
        {
            long len = stream.Length;
            byte[] buffer = new byte[len];
            int cnt = stream.Read(buffer, 0, (int)len);
            stream.Close();
            byte[] data = new byte[cnt];
            Array.Copy(buffer, data, cnt);
            uploadData(httpManager, data, key, token, uploadOptions, upCompletionHandler);
        }

        //以multipart/form-data方式上传文件，可以指定key，也可以设置为null
        public static void uploadFile(HttpManager httpManager, string filePath, string key,
            string token, UploadOptions uploadOptions, UpCompletionHandler upCompletionHandler)
        {
            PostArgs postArgs = new PostArgs();
            postArgs.File = filePath;
            postArgs.FileName = Path.GetFileName(filePath);
            postArgs.Params = new Dictionary<string, string>();
            //set file crc32 check
            if (uploadOptions != null && uploadOptions.CheckCrc32)
            {
                postArgs.Params.Add("crc32", new CRC32_Hsr().HashFile(filePath));
            }
            httpManager.UseData = false;
            upload(httpManager, postArgs, key, token, uploadOptions, upCompletionHandler);
        }

        private static void upload(HttpManager httpManager, PostArgs postArgs, string key, string token,
            UploadOptions uploadOptions, UpCompletionHandler upCompletionHandler)
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
            //set upload progress handler and completion handler
            ProgressHandler progressHandler = null;
            CompletionHandler completionHandler = null;
            if (uploadOptions != null && uploadOptions.UpProgressHandler != null)
            {
                progressHandler = new FormProgressHandler(uploadOptions.UpProgressHandler);
            }
            if (upCompletionHandler != null)
            {
                completionHandler = new FormCompletionHandler(upCompletionHandler);
            }
            //set http manager
            httpManager.ProgressHandler = progressHandler;
            httpManager.CompletionHandler = completionHandler;
            httpManager.PostArgs = postArgs;
            httpManager.multipartPost(Config.UPLOAD_HOST);
        }

        class FormProgressHandler : ProgressHandler
        {
            public UpProgressHandler upProgressHandler;
            public FormProgressHandler(UpProgressHandler upProgressHandler)
            {
                this.upProgressHandler = upProgressHandler;
            }

            public void progress(int bytesWritten, int totalBytes)
            {
                this.upProgressHandler.progress(bytesWritten, totalBytes);
            }
        }

        class FormCompletionHandler : CompletionHandler
        {
            private UpCompletionHandler upCompletionHandler;
            public FormCompletionHandler(UpCompletionHandler upCompletionHandler)
            {
                this.upCompletionHandler = upCompletionHandler;
            }

            public void complete(ResponseInfo info, string response)
            {
                upCompletionHandler.complete(info, response);
            }
        }
    }
}