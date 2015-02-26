using System;
using System.Collections.Generic;
using System.Text;
using Qiniu.Common;
using System.Net;
using System.Threading;
using System.IO;
using Newtonsoft.Json;
using System.IO.IsolatedStorage;
namespace Qiniu.Http
{
    public class HttpManager
    {
        private HttpWebRequest webRequest;
        private ManualResetEvent allDone = new ManualResetEvent(false);
        private const string APPLICATION_OCTET_STREAM = "application/octet-stream";
        private const string APPLICATION_FORM_URLENCODED = "application/x-www-form-urlencoded";
        private const string APPLICATION_MULTIPART_FORM = "multipart/form-data";
        private const string MULTIPART_BOUNDARY = "-------WindowsPhoneBoundaryjEdoki6WbQVQuakI";
        private const string MULTIPART_BOUNDARY_SEP_TAG = "--";
        private const string MULTIPART_SEP_LINE = "\r\n";
        private const int BUFFER_SIZE = 4096;//4KB
        private TimeSpan timeout;
        public PostContentType FileContentType { set; get; }
        public PostArgs PostArgs { set; get; }
        public WebHeaderCollection Headers { set; get; }
        public ProgressHandler ProgressHandler { set; get; }
        public CompletionHandler CompletionHandler { set; get; }
        public CancellationSignal CancellationSignal { set; get; }
        private MemoryStream postDataMemoryStream;
        private double duration;
        private DateTime startTime;
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
            this.FileContentType = PostContentType.STREAM;
            this.Headers = new WebHeaderCollection();
        }

        public void setAuthHeader(string upToken)
        {
            this.Headers[HttpRequestHeader.Authorization] = upToken;
        }

        /**
         * 以POST方式发送form-urlencoded请求
         */
        public void post(string url)
        {
            this.webRequest = (HttpWebRequest)WebRequest.CreateHttp(url);
            this.webRequest.UserAgent = this.getUserAgent();
            this.webRequest.AllowAutoRedirect = false;
            this.webRequest.Method = "POST";
            this.webRequest.ContentType = APPLICATION_FORM_URLENCODED;
            if (this.webRequest.Headers == null)
            {
                this.webRequest.Headers = new WebHeaderCollection();
            }
            foreach (string headerKey in this.Headers.AllKeys)
            {
                this.webRequest.Headers[headerKey] = this.Headers[headerKey];
            }
            //prepare data
            StringBuilder postParams = new StringBuilder();
            if (this.PostArgs != null && this.PostArgs.Params != null)
            {
                foreach (KeyValuePair<string, string> kvp in this.PostArgs.Params)
                {
                    postParams.Append(HttpUtility.UrlEncode(kvp.Key)).Append("=").Append(HttpUtility.UrlEncode(kvp.Value)).Append("&");
                }
            }
            byte[] postData = new byte[0];
            if (postParams.Length > 0)
            {
                postData = Encoding.UTF8.GetBytes(postParams.ToString().Substring(0, postParams.Length - 1));
            }
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
            this.startTime = DateTime.Now;
            HttpWebRequest request = (HttpWebRequest)asyncResult.AsyncState;
            if (postDataMemoryStream.Length > 0)
            {
                Stream postStream = request.EndGetRequestStream(asyncResult);
                postDataMemoryStream.CopyTo(postStream, (int)postDataMemoryStream.Length);
                postDataMemoryStream.Close();
                postStream.Flush();
                postStream.Close();
            }
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
            this.webRequest.Method = "POST";
            this.webRequest.ContentType = APPLICATION_OCTET_STREAM;
            this.webRequest.ContentLength = this.PostArgs.Data.Length;
            if (this.webRequest.Headers == null)
            {
                this.webRequest.Headers = new WebHeaderCollection();
            }
            foreach (string headerKey in this.Headers.AllKeys)
            {
                this.webRequest.Headers[headerKey] = this.Headers[headerKey];
            }
            this.webRequest.BeginGetRequestStream(new AsyncCallback(firePostDataRequest), webRequest);
            allDone.WaitOne(timeout);
        }

        private void firePostDataRequest(IAsyncResult asyncResult)
        {
            this.startTime = DateTime.Now;
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
                //check cancellation signal
                if (CancellationSignal != null && CancellationSignal())
                {
                    if (CompletionHandler != null)
                    {
                        CompletionHandler(ResponseInfo.cancelled(), "");
                    }
                    postDataStream.Close();
                    return;
                }
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
                    ProgressHandler(bytesWritten, totalBytes);
                }
            }
            postDataStream.Flush();
            postDataStream.Close();
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
            this.webRequest.Method = "POST";
            this.webRequest.ContentType = string.Format("{0}; boundary={1}", APPLICATION_MULTIPART_FORM, MULTIPART_BOUNDARY);
            //prepare data
            this.postDataMemoryStream = new MemoryStream();
            //write params
            byte[] boundarySepTag = Encoding.UTF8.GetBytes(MULTIPART_BOUNDARY_SEP_TAG);
            byte[] boundaryData = Encoding.UTF8.GetBytes(MULTIPART_BOUNDARY);
            byte[] multiPartSepLineData = Encoding.UTF8.GetBytes(MULTIPART_SEP_LINE);

            if (this.PostArgs != null && this.PostArgs.Params != null)
            {
                foreach (KeyValuePair<string, string> kvp in this.PostArgs.Params)
                {
                    //write boundary start
                    postDataMemoryStream.Write(boundarySepTag, 0, boundarySepTag.Length);
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
            }
            //write filename and mimetype header
            postDataMemoryStream.Write(boundarySepTag, 0, boundarySepTag.Length);
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
            if (FileContentType == PostContentType.BYTES)
            {
                postDataMemoryStream.Write(this.PostArgs.Data, 0, this.PostArgs.Data.Length);
            }
            else if (FileContentType == PostContentType.FILE)
            {
                try
                {
                    using (FileStream fs = IsolatedStorageFile.GetUserStoreForApplication()
                        .OpenFile(this.PostArgs.File, FileMode.Open, FileAccess.Read))
                    {
                        byte[] buffer = new byte[BUFFER_SIZE];
                        int numRead = -1;
                        while ((numRead = fs.Read(buffer, 0, buffer.Length)) != 0)
                        {
                            postDataMemoryStream.Write(buffer, 0, numRead);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (this.CompletionHandler != null)
                    {
                        this.CompletionHandler(ResponseInfo.fileError(ex), "");
                    }
                    return;
                }
            }
            else if (FileContentType == PostContentType.STREAM)
            {
                try
                {
                    byte[] buffer = new byte[BUFFER_SIZE];
                    int numRead = -1;
                    while ((numRead = this.PostArgs.Stream.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        postDataMemoryStream.Write(buffer, 0, numRead);
                    }
                }
                catch (Exception ex)
                {
                    if (this.CompletionHandler != null)
                    {
                        this.CompletionHandler(ResponseInfo.fileError(ex), "");
                    }
                    return;
                }
            }
            postDataMemoryStream.Write(multiPartSepLineData, 0, multiPartSepLineData.Length);
            //write last boundary
            postDataMemoryStream.Write(boundarySepTag, 0, boundarySepTag.Length);
            postDataMemoryStream.Write(boundaryData, 0, boundaryData.Length);
            postDataMemoryStream.Write(boundarySepTag, 0, boundarySepTag.Length);
            postDataMemoryStream.Write(multiPartSepLineData, 0, multiPartSepLineData.Length);
            postDataMemoryStream.Flush();
            this.webRequest.ContentLength = postDataMemoryStream.Length;
            if (this.webRequest.Headers == null)
            {
                this.webRequest.Headers = new WebHeaderCollection();
            }
            foreach (string headerKey in this.Headers.AllKeys)
            {
                this.webRequest.Headers[headerKey] = this.Headers[headerKey];
            }
            this.webRequest.BeginGetRequestStream(new AsyncCallback(fireMultipartPostRequest), webRequest);
            allDone.WaitOne(timeout);
        }

        private void fireMultipartPostRequest(IAsyncResult asyncResult)
        {
            this.startTime = DateTime.Now;
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
            byte[] memBuffer = new byte[BUFFER_SIZE];
            while ((memNumRead = postDataMemoryStream.Read(memBuffer, 0, memBuffer.Length)) != 0)
            {
                //check cancellation signal
                if (this.CancellationSignal != null && this.CancellationSignal())
                {
                    if (this.CompletionHandler != null)
                    {
                        this.CompletionHandler(ResponseInfo.cancelled(), "");
                    }
                    postDataStream.Close();
                    return;
                }
                postDataStream.Write(memBuffer, 0, memNumRead);
                bytesWritten += memNumRead;
                if (ProgressHandler != null)
                {
                    ProgressHandler(bytesWritten, totalBytes);
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
            int statusCode = ResponseInfo.NetworkError;
            string reqId = null;
            string xlog = null;
            string ip = null;
            string xvia = null;
            string error = null;
            string host = null;
            string respData = null;
            HttpWebResponse response = null;
            try
            {
                response = (HttpWebResponse)request.EndGetResponse(asyncResult);
            }
            catch (WebException wex)
            {
                if (wex.Response != null)
                {
                    response = (HttpWebResponse)wex.Response;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            if (response != null)
            {
                statusCode = (int)response.StatusCode;
                if (response.Headers != null)
                {
                    WebHeaderCollection respHeaders = response.Headers;
                    foreach (string headerName in respHeaders.AllKeys)
                    {
                        if (headerName.Equals("X-Reqid"))
                        {
                            reqId = respHeaders[headerName].ToString();
                        }
                        else if (headerName.Equals("X-Log"))
                        {
                            xlog = respHeaders[headerName].ToString();
                        }
                        else if (headerName.Equals("X-Via"))
                        {
                            xvia = respHeaders[headerName].ToString();
                        }
                        else if (headerName.Equals("X-Px"))
                        {
                            xvia = respHeaders[headerName].ToString();
                        }
                        else if (headerName.Equals("Fw-Via"))
                        {
                            xvia = respHeaders[headerName].ToString();
                        }
                        else if (headerName.Equals("Host"))
                        {
                            host = respHeaders[headerName].ToString();
                        }
                    }
                    using (StreamReader respStream = new StreamReader(response.GetResponseStream()))
                    {
                        respData = respStream.ReadToEnd();
                        if (respData != null)
                        {
                            try
                            {
                                Dictionary<string, string> respErrorDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(respData);
                                if (respErrorDict != null && respErrorDict.ContainsKey("error"))
                                {
                                    error = respErrorDict["error"];
                                }
                            }
                            catch (Exception)
                            {
                                error = respData;
                            }
                        }
                        else
                        {
                            error = "no response";
                        }

                    }
                    ip = webRequest.RequestUri.Authority;
                    response.Close();
                }
            }

            duration = DateTime.Now.Subtract(this.startTime).TotalSeconds;
            ResponseInfo respInfo = new ResponseInfo(statusCode, reqId, xlog, xvia, host, ip, duration, error);
            if (this.CompletionHandler != null)
            {
                this.CompletionHandler(respInfo, respData);
            }
            allDone.Set();
        }
    }
}
