using Qiniu.Util;
using System;
using System.IO;
using System.IO.IsolatedStorage;

namespace Qiniu.Storage.Persistent
{
    /// <summary>
    /// 分片上传进度记录器
    /// </summary>
    public class ResumeRecorder
    {
        //上传进度记录目录
        private string dir;
        //沙盒存储对象
        private IsolatedStorageFile storage;

        /// <summary>
        /// 构建上传进度记录器
        /// </summary>
        /// <param name="dir">沙盒目录</param>
        public ResumeRecorder(string dir)
        {
            this.dir = dir;
            this.storage = IsolatedStorageFile.GetUserStoreForApplication();
            createDirIfNotExist();
        }

        /// <summary>
        /// 如果指定沙盒目录不存在，则创建
        /// </summary>
        private void createDirIfNotExist()
        {
            string[] matches = this.storage.GetDirectoryNames(dir);
            if (matches.Length == 0)
            {
                this.storage.CreateDirectory(dir);
            }
        }

        /// <summary>
        /// 写入或更新上传进度记录
        /// </summary>
        /// <param name="key">记录文件名</param>
        /// <param name="data">上传进度数据</param>
        public void set(string key, byte[] data)
        {
            string filePath = Path.Combine(this.dir, StringUtils.urlSafeBase64Encode(key));
            using (IsolatedStorageFileStream stream =
                new IsolatedStorageFileStream(filePath, FileMode.Create, this.storage))
            {
                stream.Write(data, 0, data.Length);
                stream.Flush();
            }
        }

        /// <summary>
        /// 获取上传进度记录
        /// </summary>
        /// <param name="key">记录文件名</param>
        /// <returns>上传进度数据</returns>
        public byte[] get(string key)
        {
            byte[] data = null;
            string filePath = Path.Combine(this.dir, StringUtils.urlSafeBase64Encode(key));
            try
            {
                using (IsolatedStorageFileStream stream =
                    new IsolatedStorageFileStream(filePath, FileMode.Open, this.storage))
                {
                    data = new byte[stream.Length];
                    stream.Read(data, 0, data.Length);
                }
            }
            catch (Exception)
            {

            }
            return data;
        }

        /// <summary>
        /// 删除上传进度记录
        /// </summary>
        /// <param name="key">记录文件名</param>
        public void del(string key)
        {
            string filePath = Path.Combine(this.dir, StringUtils.urlSafeBase64Encode(key));
            try
            {
                this.storage.DeleteFile(filePath);
            }
            catch (Exception)
            {

            }
        }
    }
}
