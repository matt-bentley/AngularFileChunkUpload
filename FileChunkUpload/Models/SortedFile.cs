using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileChunkUpload.Models
{
    public class SortedFile
    {
        public string FileName { get; set; }
        public int FileOrder { get; set; }
        public byte[] Bytes { get; set; }
    }
}
