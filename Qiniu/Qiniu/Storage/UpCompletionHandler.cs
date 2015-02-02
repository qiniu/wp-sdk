using Qiniu.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Qiniu.Storage
{
    public abstract class UpCompletionHandler
    {
        public abstract void complete(ResponseInfo info, string response);
    }
}
