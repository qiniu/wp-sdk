using Qiniu.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Qiniu.Http
{
    public delegate void ProgressCallback(int bytesWritten, int totalBytes);
}