using Qiniu.Util;
using System;
using System.IO;
using System.IO.IsolatedStorage;

namespace Qiniu.Storage.Persistent
{
    //存储在IsolatedStorage里面的分片进度
    public class ResumeRecorder
    {
        private string dir;
        private IsolatedStorageFile storage;
        public ResumeRecorder(string dir)
        {
            this.dir = dir;
            this.storage = IsolatedStorageFile.GetUserStoreForApplication();
            createDirIfNotExist();
        }

        private void createDirIfNotExist()
        {
            string[] matches = this.storage.GetDirectoryNames(dir);
            if (matches.Length == 0)
            {
                this.storage.CreateDirectory(dir);
            }
        }

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
