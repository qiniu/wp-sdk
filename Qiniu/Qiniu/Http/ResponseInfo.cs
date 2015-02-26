using System;

namespace Qiniu.Http
{
    public class ResponseInfo
    {
        public const int InvalidArgument = -4;
        public const int InvalidFile = -3;
        public const int Cancelled = -2;
        public const int NetworkError = -1;

        public int StatusCode { set; get; }
        public string ReqId { set; get; }
        public string Xlog { set; get; }
        public string Xvia { set; get; }
        public string Error { set; get; }
        public double Duration { set; get; }
        public string Host { set; get; }
        public string Ip { set; get; }

        public ResponseInfo(int statusCode, string reqId, string xlog, string xvia,
            string host, string ip, double duration, string error)
        {
            this.StatusCode = statusCode;
            this.ReqId = reqId;
            this.Xlog = xlog;
            this.Xvia = xvia;
            this.Host = host;
            this.Ip = ip;
            this.Duration = duration;
            this.Error = error;
        }

        public static ResponseInfo cancelled()
        {
            return new ResponseInfo(Cancelled, "", "", "", "", "", 0, "cancelled by user");
        }

        public static ResponseInfo invalidArgument(string message)
        {
            return new ResponseInfo(InvalidArgument, "", "", "", "", "", 0, message);
        }

        public static ResponseInfo fileError(Exception e)
        {
            return new ResponseInfo(InvalidFile, "", "", "", "", "", 0, e.Message);
        }

        public bool isCancelled()
        {
            return StatusCode == Cancelled;
        }

        public bool isOk()
        {
            return StatusCode == 200 && Error == null && ReqId != null;
        }

        public bool isNetworkBroken()
        {
            return StatusCode == NetworkError;
        }

        public bool isServerError()
        {
            return (StatusCode >= 500 && StatusCode < 600 && StatusCode != 579) || StatusCode == 996;
        }

        public bool needSwitchServer()
        {
            return isNetworkBroken() || (StatusCode >= 500 && StatusCode < 600 && StatusCode != 579);
        }

        public bool needRetry()
        {
            return isNetworkBroken() || isServerError() || StatusCode == 406 || (StatusCode == 200 && Error != null);
        }

        private string toStr(string val)
        {
            return (val == null) ? "null" : val;
        }

        public override string ToString()
        {
            return string.Format("ResponseInfo: status:{0}, reqId:{1}, xlog:{2}, xvia:{3}, host:{4}, ip:{5}, duration:{6} s, error:{7}",
                 StatusCode, toStr(ReqId), toStr(Xlog), toStr(Xvia), toStr(Host), toStr(Ip), Duration, toStr(Error));
        }
    }
}
