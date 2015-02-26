using Newtonsoft.Json;
using Qiniu.Util;
namespace Qiniu.Storage.Persistent
{
    public class ResumeRecord
    {
        public long Size { get; private set; }
        public long Offset { get; private set; }
        public long LastModified { get; private set; }
        public string[] Contexts { get; private set; }

        public ResumeRecord(long size, long offset, long lastModified, string[] contexts)
        {
            this.Size = size;
            this.Offset = offset;
            this.LastModified = lastModified;
            this.Contexts = contexts;
        }

        public static ResumeRecord fromJsonData(string jsonData)
        {
            ResumeRecord record = JsonConvert.DeserializeObject<ResumeRecord>(jsonData);
            return record;
        }

        public string toJsonData()
        {
            return string.Format("{{\"size\":{0}, \"offset\":{1}, \"modify_time\":{2}, \"contexts\":[{3}]}}",
                this.Size, this.Offset, this.LastModified, StringUtils.jsonJoin(this.Contexts));
        }
    }
}
