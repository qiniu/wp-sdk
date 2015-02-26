
namespace Qiniu.Common
{
    public class Config
    {
        //SDK的版本号
        public const string VERSION = "1.0.0";

        //默认上传服务器地址
        public const string UPLOAD_HOST = "http://upload.qiniu.com";

        //上传重试服务器地址
        public const string UP_HOST = "http://up.qiniu.com";

        //分片上传切片大小
        public const int CHUNK_SIZE = 256 * 1024;

        //分片上传块的大小，固定为4M，不可修改
        public const int BLOCK_SIZE = 4 * 1024 * 1024;

        //上传失败重试次数
        public const int RETRY_MAX = 5;

        //分片上传的阈值，文件超过该大小采用分片上传
        public const int PUT_THRESHOLD = 512 * 1024;

        //回复超时时间，单位秒
        public const int TIMEOUT_INTERVAL = 30;
    }
}
