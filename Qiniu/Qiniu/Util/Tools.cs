using Newtonsoft.Json;

namespace Qiniu.Util
{
    class Tools
    {
        public static string jsonEncode(object obj)
        {
            JsonSerializerSettings setting = new JsonSerializerSettings();
            setting.NullValueHandling = NullValueHandling.Ignore;
            return JsonConvert.SerializeObject(obj, setting);
        }

        public static T getJsonObject<T>(string value)
        {
            return JsonConvert.DeserializeObject<T>(value);
        }
    }
}
