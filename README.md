# wp-sdk

```
using System.Windows;
using Microsoft.Phone.Controls;
using Qiniu.Http;
using Qiniu.Storage;
using System.Text;
using Qiniu.Common;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using Newtonsoft.Json;
using Microsoft.Xna.Framework.Media;

namespace QiniuLab
{
    public partial class MainPage : PhoneApplicationPage
    {
        // Constructor
        public MainPage()
        {
            InitializeComponent();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            HttpManager h = new HttpManager();
            string tokenUrl = "http://localtunnel.qiniu.io:9090/demos/api/simple_upload_without_key_upload_token.php";
            h.CompletionHandler = new SimpleUploadWithoutKey();
            h.post(tokenUrl);
        }

        class SimpleUploadWithoutKey : CompletionHandler
        {
            public void complete(ResponseInfo info, string response)
            {
                if (info.StatusCode == 200)
                {
                    Dictionary<string, string> respDict =
                        JsonConvert.DeserializeObject<Dictionary<string, string>>(response);
                    if (respDict.ContainsKey("uptoken"))
                    {
                        string upToken = respDict["uptoken"];
                        byte[] upData = Encoding.UTF8.GetBytes("hello 七牛云存储!");
                        UploadOptions uploadOptions = new UploadOptions(null, null, false, new SimpleUploadWithoutKeyProgressHandler(), null);
                        //FormUploader.uploadData(new HttpManager(), upData, null, upToken, uploadOptions, new SimpleUploadWithoutKeyCompletionHandler());
                        MediaLibrary ml = new MediaLibrary();
                        PictureCollection pics=ml.Pictures;
                        Picture pic=pics[0];
                        string picName = pic.Name;
                        FormUploader.uploadStream(new HttpManager(), pic.GetImage(), picName, upToken, uploadOptions, new SimpleUploadWithoutKeyCompletionHandler());
                    }
                }
            }
        }

        class SimpleUploadWithoutKeyProgressHandler : UpProgressHandler
        {

            public override void progress(int bytesWritten, int totalBytes)
            {
                Debug.WriteLine(bytesWritten + "\t" + totalBytes);
            }
        }

        class SimpleUploadWithoutKeyCompletionHandler : UpCompletionHandler
        {

            public override void complete(ResponseInfo info, string response)
            {
                if (info.isOk())
                {
                    string hash = null;
                    string key = null;
                    Dictionary<string, string> respDict =
                        JsonConvert.DeserializeObject<Dictionary<string, string>>(response);
                    if (respDict.ContainsKey("hash"))
                    {
                        hash = respDict["hash"];
                    }
                    if (respDict.ContainsKey("key"))
                    {
                        key = respDict["key"];
                    }
                    Debug.WriteLine("Hash:" + hash + "\tKey:" + key);

                }
                else
                {
                    Console.WriteLine("Code:" + info.StatusCode + "\t" + info.Error);
                }
            }
        }
    }
}
```
