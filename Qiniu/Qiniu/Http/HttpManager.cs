using System;
using System.Collections.Generic;
using System.Text;
using Qiniu.Common;
using System.Net;
using System.Threading;
using System.IO;
namespace Qiniu.Http
{
    public class HttpManager
    {
        private HttpWebRequest webRequest;
        private ManualResetEvent allDone = new ManualResetEvent(false);
        private const string APPLICATION_OCTET_STREAM = "application/octet-stream";
        private const string APPLICATION_FORM_URLENCODED = "application/x-www-form-urlencoded";
        private const string APPLICATION_MULTIPART_FORM = "multipart/form-data";
        private const string MULTIPART_BOUNDARY = "WindowsPhoneBoundaryjEdoki6WbQVQuakI";
        private const string MULTIPART_BOUNDARY_START_TAG="--";
        private const string MULTIPART_BOUNDARY_END_TAG = "--";
        private const string MULTIPART_SEP_LINE = "\r\n";
        private const int BUFFER_SIZE = 4096;//4KB
        private TimeSpan timeout;
        public bool UseData { set; get; }
        public PostArgs PostArgs { set; get; }
        public WebHeaderCollection Headers { set; get; }
        public ProgressHandler ProgressHandler { set; get; }
        public CompletionHandler CompletionHandler { set; get; }
        private MemoryStream postDataMemoryStream;
        private string genId()
        {
            Random r = new Random();
            return System.DateTime.Now.Millisecond + "" + r.Next(999);
        }

        private string getUserAgent()
        {
            return string.Format("QiniuWindowsPhone/{0} ({1}; {2}; {3}; {4}; {5})",
                Config.VERSION,
                Microsoft.Phone.Info.DeviceStatus.DeviceName,
                Microsoft.Phone.Info.DeviceStatus.DeviceHardwareVersion,
                Microsoft.Phone.Info.DeviceStatus.DeviceFirmwareVersion,
                Microsoft.Phone.Info.DeviceStatus.DeviceManufacturer,
                genId());
        }

        public HttpManager()
        {
            this.timeout = new TimeSpan(0, 0, 0, Config.TIMEOUT_INTERVAL);
            this.UseData = false;
            this.Headers = new WebHeaderCollection();
        }

        /**
         * 以POST方式发送form-urlencoded请求
         */
        public void post(string url)
        {
            this.webRequest = (HttpWebRequest)WebRequest.CreateHttp(url);
            this.webRequest.UserAgent = this.getUserAgent();
            this.webRequest.AllowAutoRedirect = false;
            this.webRequest.Headers = Headers;
            this.webRequest.Method = "POST";
            this.webRequest.ContentType = APPLICATION_FORM_URLENCODED;
            //prepare data
            StringBuilder postParams = new StringBuilder();
            foreach (KeyValuePair<string, string> kvp in this.PostArgs.Params)
            {
                postParams.Append(HttpUtility.UrlEncode(kvp.Key)).Append("=").Append(HttpUtility.UrlEncode(kvp.Value)).Append("&");
            }
            byte[] postData = Encoding.UTF8.GetBytes(postParams.ToString().Substring(0, postParams.Length - 1));
            this.postDataMemoryStream = new MemoryStream(postData);
            //set content length
            this.webRequest.ContentLength = this.postDataMemoryStream.Length;
            this.webRequest.AllowWriteStreamBuffering = true;
            this.webRequest.AllowReadStreamBuffering = true;
            this.webRequest.BeginGetRequestStream(new AsyncCallback(firePostRequest),
                this.webRequest);
            allDone.WaitOne(timeout);
        }

        private void firePostRequest(IAsyncResult asyncResult)
        {
            HttpWebRequest request = (HttpWebRequest)asyncResult.AsyncState;
            Stream postStream = request.EndGetRequestStream(asyncResult);
            postDataMemoryStream.CopyTo(postStream, (int)postDataMemoryStream.Length);
            postDataMemoryStream.Close();
            postStream.Flush();
            postStream.Close();
            request.BeginGetResponse(new AsyncCallback(handleResponse), request);
        }

        /**
         * 传输二进制数据，用在分片上传中 
         */
        public void postData(string url)
        {
            this.webRequest = (HttpWebRequest)WebRequest.CreateHttp(url);
            this.webRequest.UserAgent = this.getUserAgent();
            this.webRequest.AllowAutoRedirect = false;
            this.webRequest.Headers = Headers;
            this.webRequest.Method = "POST";
            this.webRequest.ContentType = APPLICATION_OCTET_STREAM;
            this.webRequest.BeginGetRequestStream(new AsyncCallback(firePostDataRequest), webRequest);
            allDone.WaitOne(timeout);
        }

        private void firePostDataRequest(IAsyncResult asyncResult)
        {
            HttpWebRequest request = (HttpWebRequest)asyncResult.AsyncState;
            Stream postDataStream = request.EndGetRequestStream(asyncResult);

            int totalBytes = this.PostArgs.Data.Length;
            int writeTimes = totalBytes / BUFFER_SIZE;
            int bytesWritten = 0;
            if (totalBytes % BUFFER_SIZE != 0)
            {
                writeTimes += 1;
            }
            for (int i = 0; i < writeTimes; i++)
            {
                int offset = i * BUFFER_SIZE;
                int size = BUFFER_SIZE;
                if (i == writeTimes - 1)
                {
                    size = totalBytes - i * BUFFER_SIZE;
                }
                postDataStream.Write(this.PostArgs.Data, offset, size);
                bytesWritten += size;
                if (ProgressHandler != null)
                {
                    ProgressHandler.progress(bytesWritten, totalBytes);
                }
            }
            postDataStream.Flush();
            postDataStream.Close();
            request.ContentLength = totalBytes;
            request.BeginGetResponse(new AsyncCallback(handleResponse), request);
        }

        /**
         * 以POST方式发送multipart/form-data格式数据
         */
        public void multipartPost(string url)
        {
            this.webRequest = (HttpWebRequest)WebRequest.CreateHttp(url);
            this.webRequest.UserAgent = this.getUserAgent();
            this.webRequest.AllowAutoRedirect = false;
            this.webRequest.Headers = Headers;
            this.webRequest.Method = "POST";
            this.webRequest.ContentType = string.Format("{0}; boundary={1}", APPLICATION_MULTIPART_FORM, MULTIPART_BOUNDARY);
            //prepare data
            this.postDataMemoryStream = new MemoryStream();
            //write params
            byte[] boundaryStartTag = Encoding.UTF8.GetBytes(MULTIPART_BOUNDARY_START_TAG);
            byte[] boundaryEndTag = Encoding.UTF8.GetBytes(MULTIPART_BOUNDARY_END_TAG);
            byte[] boundaryData = Encoding.UTF8.GetBytes(MULTIPART_BOUNDARY);
            byte[] multiPartSepLineData = Encoding.UTF8.GetBytes(MULTIPART_SEP_LINE);
            
            foreach (KeyValuePair<string, string> kvp in this.PostArgs.Params)
            {
                //write boundary start
                postDataMemoryStream.Write(boundaryStartTag, 0, boundaryStartTag.Length);
                //write boundary
                postDataMemoryStream.Write(boundaryData, 0, boundaryData.Length);
                //wrtie header and content
                postDataMemoryStream.Write(multiPartSepLineData, 0, multiPartSepLineData.Length);
                byte[] contentHeaderData = Encoding.UTF8.GetBytes(
                    string.Format("Content-Disposition: form-data; name=\"{0}\"", kvp.Key));
                postDataMemoryStream.Write(contentHeaderData, 0, contentHeaderData.Length);
                postDataMemoryStream.Write(multiPartSepLineData, 0, multiPartSepLineData.Length);
                postDataMemoryStream.Write(multiPartSepLineData, 0, multiPartSepLineData.Length);
                byte[] contentData = Encoding.UTF8.GetBytes(kvp.Value);
                postDataMemoryStream.Write(contentData, 0, contentData.Length);
                postDataMemoryStream.Write(multiPartSepLineData, 0, multiPartSepLineData.Length);
            }
            //write filename and mimetype header
            //write boundary start and boundary
            postDataMemoryStream.Write(boundaryStartTag, 0, boundaryStartTag.Length);
            postDataMemoryStream.Write(boundaryData, 0, boundaryData.Length);
            postDataMemoryStream.Write(multiPartSepLineData, 0, multiPartSepLineData.Length);
            string fileName = this.PostArgs.FileName;
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = string.Format("RandomFileName_{0}", genId());
            }
            byte[] fileHeaderData = Encoding.UTF8.GetBytes(
                string.Format("Content-Disposition: form-data; name=\"file\"; filename=\"{0}\"", fileName));
            string fileContentType = "application/octet-stream";
            if (!string.IsNullOrEmpty(this.PostArgs.MimeType))
            {
                fileContentType = this.PostArgs.MimeType;
            }
            byte[] fileContentTypeData = Encoding.UTF8.GetBytes(string.Format("Content-Type: {0}", fileContentType));
            postDataMemoryStream.Write(fileHeaderData, 0, fileHeaderData.Length);
            postDataMemoryStream.Write(multiPartSepLineData, 0, multiPartSepLineData.Length);
            postDataMemoryStream.Write(fileContentTypeData, 0, fileContentTypeData.Length);
            postDataMemoryStream.Write(multiPartSepLineData, 0, multiPartSepLineData.Length);
            postDataMemoryStream.Write(multiPartSepLineData, 0, multiPartSepLineData.Length);
            //write file data
            if (UseData)
            {
                postDataMemoryStream.Write(this.PostArgs.Data, 0, this.PostArgs.Data.Length);
            }
            else
            {
                using (FileStream fs = new FileStream(this.PostArgs.File, FileMode.Open, FileAccess.Read))
                {
                    byte[] buffer = new byte[BUFFER_SIZE];
                    int numRead = -1;
                    while ((numRead = fs.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        postDataMemoryStream.Write(buffer, 0, numRead);
                    }
                }
            }
            postDataMemoryStream.Write(multiPartSepLineData, 0, multiPartSepLineData.Length);
            //write last boundary
            postDataMemoryStream.Write(boundaryStartTag, 0, boundaryStartTag.Length);
            postDataMemoryStream.Write(boundaryData, 0, boundaryData.Length);
            postDataMemoryStream.Write(boundaryEndTag, 0, boundaryEndTag.Length);
            postDataMemoryStream.Write(multiPartSepLineData, 0, multiPartSepLineData.Length);
            postDataMemoryStream.Flush();
            this.webRequest.ContentLength = postDataMemoryStream.Length;
            this.webRequest.BeginGetRequestStream(new AsyncCallback(fireMultipartPostRequest), webRequest);
            allDone.WaitOne(timeout);
        }

        private void fireMultipartPostRequest(IAsyncResult asyncResult)
        {
            HttpWebRequest request = (HttpWebRequest)asyncResult.AsyncState;
            Stream postDataStream = request.EndGetRequestStream(asyncResult);
            //write to post data stream
            int bytesWritten = 0;
            int totalBytes = (int)postDataMemoryStream.Length;
            int writeTimes = totalBytes / BUFFER_SIZE;
            if (totalBytes % BUFFER_SIZE != 0)
            {
                writeTimes += 1;
            }
            //reset to begin to read
            postDataMemoryStream.Seek(0, SeekOrigin.Begin);
            int memNumRead = 0;
            byte[] memBuffer=new byte[BUFFER_SIZE];
            while ((memNumRead = postDataMemoryStream.Read(memBuffer, 0, memBuffer.Length)) != 0)
            {
                postDataStream.Write(memBuffer, 0, memNumRead);
                bytesWritten += memNumRead;
                if (ProgressHandler != null)
                {
                    ProgressHandler.progress(bytesWritten, totalBytes);
                }
            }
            //flush and close
            postDataMemoryStream.Close();
            postDataStream.Flush();
            postDataStream.Close();
            request.BeginGetResponse(new AsyncCallback(handleResponse), request);
        }

        private void handleResponse(IAsyncResult asyncResult)
        {
            HttpWebRequest request = (HttpWebRequest)asyncResult.AsyncState;
            //check for exception
            HttpWebResponse response = (HttpWebResponse)request.EndGetResponse(asyncResult);

            StreamReader respStream = new StreamReader(response.GetResponseStream());
            HttpStatusCode statusCode = response.StatusCode;
            string respData = respStream.ReadToEnd();
            bool isOk = statusCode == HttpStatusCode.OK;
            Console.WriteLine(isOk);
            //reset
            respStream.Close();
            response.Close();
            allDone.Set();
        }
    }
}
