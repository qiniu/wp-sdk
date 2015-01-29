using Qiniu.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Qiniu.Http
{
    public interface ProgressHandler
    {
        void progress(int bytesWritten, int totalBytes);
    }
}