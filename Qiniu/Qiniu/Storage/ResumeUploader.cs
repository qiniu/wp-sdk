using Newtonsoft.Json;
using Qiniu.Common;
using Qiniu.Http;
using Qiniu.Storage.Persistent;
using Qiniu.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Text;

namespace Qiniu.Storage
{
    public class ResumeUploader
    {
        private HttpManager httpManager;
        private UploadOptions uploadOptions;
        private UpCompletionHandler upCompletionHandler;
        private string key;
        private long size;
        private string[] contexts;
        private byte[] chunkBuffer;
        private ResumeRecorder resumeRecorder;
        private string recordKey;
        private long lastModifyTime;
        private string filePath;
        private long crc32;
        private Stream fileStream;
        private IsolatedStorageFile storage;

        public ResumeUploader(HttpManager httpManager, ResumeRecorder recorder, string recordKey, string filePath,
            string key, string token, UploadOptions uploadOptions, UpCompletionHandler upCompletionHandler)
        {
            this.httpManager = httpManager;
            this.resumeRecorder = recorder;
            this.recordKey = recordKey;
            this.filePath = filePath;
            this.key = key;
            this.storage = IsolatedStorageFile.GetUserStoreForApplication();
            this.uploadOptions = (uploadOptions == null) ? UploadOptions.defaultOptions() : uploadOptions;
            this.upCompletionHandler = upCompletionHandler;
            this.httpManager.setAuthHeader("UpToken " + token);
            this.chunkBuffer = new byte[Config.CHUNK_SIZE];
        }

        //发送mkblk请求
        private void makeBlock(string upHost, long offset, int blockSize, int chunkSize,
            ProgressHandler progressHandler, CompletionHandler completionHandler)
        {
            string url = string.Format("{0}/mkblk/{1}", upHost, blockSize);
            try
            {
                this.fileStream.Seek(offset, SeekOrigin.Begin);
                this.fileStream.Read(this.chunkBuffer, 0, chunkSize);
            }
            catch (Exception ex)
            {
                if (this.upCompletionHandler != null)
                {
                    this.upCompletionHandler(this.key, ResponseInfo.fileError(ex), "");
                }
                return;
            }
            this.crc32 = CRC32.CheckSumBytes(this.chunkBuffer, chunkSize);
            post(url, this.chunkBuffer, chunkSize, progressHandler, completionHandler);
        }

        //发送bput请求
        private void putChunk(string upHost, long offset, int chunkSize, string context,
            ProgressHandler progressHandler, CompletionHandler completionHandler)
        {
            int chunkOffset = (int)(offset % Config.BLOCK_SIZE);
            string url = string.Format("{0}/bput/{1}/{2}", upHost, context, chunkOffset);
            try
            {
                this.fileStream.Seek(offset, SeekOrigin.Begin);
                this.fileStream.Read(this.chunkBuffer, 0, chunkSize);
            }
            catch (Exception ex)
            {
                if (this.upCompletionHandler != null)
                {
                    this.upCompletionHandler(this.key, ResponseInfo.fileError(ex), "");
                }
                return;
            }
            this.crc32 = CRC32.CheckSumBytes(this.chunkBuffer, chunkSize);
            post(url, this.chunkBuffer, chunkSize, progressHandler, completionHandler);
        }


        //发送mkfile请求
        private void makeFile(string upHost, CompletionHandler completionHandler)
        {
            string mimeTypeStr = string.Format("/mimeType/{0}", StringUtils.urlSafeBase64Encode(this.uploadOptions.MimeType));

            string keyStr = "";
            if (this.key != null)
            {
                keyStr = string.Format("/key/{0}", StringUtils.urlSafeBase64Encode(this.key));
            }

            string paramsStr = "";
            if (this.uploadOptions.ExtraParams.Count > 0)
            {
                string[] paramArray = new string[this.uploadOptions.ExtraParams.Count];
                int j = 0;
                foreach (KeyValuePair<string, string> kvp in this.uploadOptions.ExtraParams)
                {
                    paramArray[j++] = string.Format("{0}/{1}", kvp.Key, StringUtils.urlSafeBase64Encode(kvp.Value));
                }
                paramsStr = "/" + StringUtils.join(paramArray, "/");
            }

            string url = string.Format("{0}/mkfile/{1}{2}{3}{4}", upHost, this.size, mimeTypeStr, keyStr, paramsStr);
            string postBody = StringUtils.join(this.contexts, ",");
            byte[] postBodyData = Encoding.UTF8.GetBytes(postBody);
            post(url, postBodyData, postBodyData.Length, null, completionHandler);
        }

        //发送数据
        private void post(string url, byte[] data, int chunkSize, ProgressHandler progressHandler, CompletionHandler completionHandler)
        {
            byte[] uploadData = new byte[chunkSize];
            Array.Copy(data, uploadData, chunkSize);
            PostArgs postArgs = new PostArgs();
            postArgs.Data = uploadData;
            this.httpManager.PostArgs = postArgs;
            this.httpManager.ProgressHandler = progressHandler;
            this.httpManager.CompletionHandler = completionHandler;
            this.httpManager.postData(url);
        }

        //上传文件
        public void uploadFile()
        {
            try
            {
                this.fileStream = this.storage.OpenFile(this.filePath, FileMode.Open, FileAccess.Read);
                this.lastModifyTime = this.storage.GetLastWriteTime(this.filePath).ToFileTime();
                this.size = this.fileStream.Length;
                long blockCount = (this.size % Config.BLOCK_SIZE == 0) ? (this.size / Config.BLOCK_SIZE) : (this.size / Config.BLOCK_SIZE + 1);
                this.contexts = new string[blockCount];
            }
            catch (Exception ex)
            {
                if (this.upCompletionHandler != null)
                {
                    this.upCompletionHandler(this.key, ResponseInfo.fileError(ex), "");
                }
                return;
            }

            long offset = recoveryFromResumeRecord();
            this.nextTask(offset, 0, Config.UPLOAD_HOST);
        }

        private long recoveryFromResumeRecord()
        {
            long offset = 0;
            if (this.resumeRecorder != null)
            {
                byte[] data = this.resumeRecorder.get(this.recordKey);
                ResumeRecord r = ResumeRecord.fromJsonData(Encoding.UTF8.GetString(data,0,data.Length));
                offset = r.Offset;
                for (int i = 0; i < r.Contexts.Length; i++)
                {
                    this.contexts[i] = r.Contexts[i];
                }
            }
            return offset;
        }

        private void record(long offset)
        {
            if (this.resumeRecorder == null || offset == 0)
            {
                return;
            }
            ResumeRecord r = new ResumeRecord(this.size, offset, this.lastModifyTime, this.contexts);
            this.resumeRecorder.set(this.recordKey,Encoding.UTF8.GetBytes(r.toJsonData()));
        }

        private void removeRecord()
        {
            if (this.resumeRecorder != null)
            {
                this.resumeRecorder.del(this.key);
            }
        }

        private bool isCancelled()
        {
            return this.uploadOptions.CancellationSignal();
        }

        private int calcBPutChunkSize(long offset)
        {
            int left = (int)(this.size - offset);
            return left < Config.CHUNK_SIZE ? left : Config.CHUNK_SIZE;
        }

        private int calcMakeBlockSize(long offset)
        {
            int left = (int)(this.size - offset);
            return left < Config.BLOCK_SIZE ? left : Config.BLOCK_SIZE;
        }

        private void nextTask(long offset, int retried, string upHost)
        {
            //上传中途触发停止
            if (this.isCancelled())
            {
                this.upCompletionHandler(this.key, ResponseInfo.cancelled(), null);
                return;
            }
            //所有分片已上传
            if (offset == this.size)
            {
                this.makeFile(upHost, new CompletionHandler(delegate(ResponseInfo respInfo, string response)
                {
                    //makeFile成功
                    if (respInfo.isOk())
                    {
                        removeRecord();
                        this.upCompletionHandler(this.key, respInfo, response);
                        return;
                    }

                    //失败重试
                    if (respInfo.needRetry() && retried < Config.RETRY_MAX)
                    {
                        string upHost2 = upHost;
                        if (respInfo.needSwitchServer())
                        {
                            upHost2 = Config.UP_HOST;
                        }
                        nextTask(offset, retried + 1, upHost2);
                        return;
                    }

                    this.upCompletionHandler(key, respInfo, response);
                }));
                return;
            }

            //创建块或上传分片
            int chunkSize = calcBPutChunkSize(offset);
            ProgressHandler progressHandler = new ProgressHandler(delegate(int bytesWritten, int totalBytes)
            {
                double percent = (double)(offset + bytesWritten) / this.size;
                this.uploadOptions.ProgressHandler(this.key, percent);
            });

            CompletionHandler completionHandler = new CompletionHandler(delegate(ResponseInfo respInfo, string response)
            {
                if (!respInfo.isOk())
                {
                    //如果是701错误，为mkblk的ctx过期
                    if (respInfo.StatusCode == 701)
                    {
                        nextTask((offset / Config.BLOCK_SIZE) * Config.BLOCK_SIZE, retried, upHost);
                        return;
                    }

                    if (retried >= Config.RETRY_MAX || !respInfo.needRetry())
                    {
                        this.upCompletionHandler(key, respInfo, response);
                        return;
                    }

                    String upHost2 = upHost;
                    if (respInfo.needSwitchServer())
                    {
                        upHost2 = Config.UP_HOST;
                    }
                    nextTask(offset, retried + 1, upHost2);
                    return;
                }

                //请求成功
                string chunkContext = null;
                if (response == null || string.IsNullOrEmpty(response))
                {
                    nextTask(offset, retried + 1, upHost);
                    return;
                }

                long chunkCrc32 = 0;
                Dictionary<string, string> respDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(response);
                if (respDict.ContainsKey("ctx"))
                {
                    chunkContext = respDict["ctx"];
                }
                if (respDict.ContainsKey("crc32"))
                {
                    chunkCrc32 = Convert.ToInt64(respDict["crc32"]);
                }

                if (chunkContext == null || chunkCrc32 != this.crc32)
                {
                    nextTask(offset, retried + 1, upHost);
                    return;
                }

                this.contexts[offset / Config.BLOCK_SIZE] = chunkContext;
                record(offset + chunkSize);
                nextTask(offset + chunkSize, retried, upHost);
            });

            if (offset % Config.BLOCK_SIZE == 0)
            {
                int blockSize = calcMakeBlockSize(offset);
                this.makeBlock(upHost, offset, blockSize, chunkSize, progressHandler, completionHandler);
                return;
            }

            string context = this.contexts[offset / Config.BLOCK_SIZE];
            this.putChunk(upHost, offset, chunkSize, context, progressHandler, completionHandler);
        }
    }
}