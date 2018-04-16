using FileChunkUpload.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FileChunkUpload.Controllers
{
    [Route("api/[controller]")]
    public class FilesController : Controller
    {
        private IMemoryCache _cache;

        public FilesController(IMemoryCache memoryCache)
        {
            _cache = memoryCache;
        }

        [HttpPost("UploadChunk")]
        public IActionResult UploadChunk(IFormFile fileBase)
        {
            foreach (var file in Request.Form.Files)
            {
                if (file != null && file.Length > 0)
                {
                    // take the input stream, and save it to a temp folder using  
                    // the original file.part name posted  
                    var stream = file.OpenReadStream();
                    var fileName = Path.GetFileName(file.FileName);
                    var uploadPath = "App_Data\\uploads";
                    Directory.CreateDirectory(uploadPath);
                    string path = Path.Combine(uploadPath, fileName);
                    try
                    {
                        if (System.IO.File.Exists(path))
                            System.IO.File.Delete(path);
                        using (var fileStream = System.IO.File.Create(path))
                        {
                            stream.CopyTo(fileStream);
                        }
                        // Once the file part is saved, see if we have enough to merge it  
                        MergeFile(path);
                    }
                    catch (IOException ex)
                    {
                        // handle  
                    }
                }
            }
            return Ok();
        }

        private bool MergeFile(string FileName)
        {
            bool rslt = false;
            // parse out the different tokens from the filename according to the convention
            string partToken = ".part_";

            string baseFileName = FileName.Substring(0, FileName.IndexOf(partToken));
            string trailingTokens = FileName.Substring(FileName.IndexOf(partToken) + partToken.Length);

            int fileIndex = 0;
            int fileCount = 0;
            int.TryParse(trailingTokens.Substring(0, trailingTokens.IndexOf(".")), out fileIndex);
            int.TryParse(trailingTokens.Substring(trailingTokens.IndexOf(".") + 1), out fileCount);

            // get a list of all file parts in the temp folder
            string searchpattern = Path.GetFileName(baseFileName) + partToken + "*";
            string[] filesList = Directory.GetFiles(Path.GetDirectoryName(FileName), searchpattern);

            //  merge .. improvement would be to confirm individual parts are there / correctly in
            // sequence, a security check would also be important
            // only proceed if we have received all the file chunks
            if (filesList.Count() == fileCount)
            {
                // use a singleton to stop overlapping processes
                //if (!MergeFileManager.Instance.InUse(baseFileName))
                //{
                //MergeFileManager.Instance.AddFile(baseFileName);
                if (System.IO.File.Exists(baseFileName))
                {
                    System.IO.File.Delete(baseFileName);
                }

                // add each file located to a list so we can get them into
                // the correct order for rebuilding the file
                List<SortedFile> mergeList = new List<SortedFile>();
                foreach (string file in filesList)
                {
                    SortedFile sFile = new SortedFile();
                    sFile.FileName = file;
                    baseFileName = file.Substring(0, file.IndexOf(partToken));
                    trailingTokens = file.Substring(file.IndexOf(partToken) + partToken.Length);
                    int.TryParse(trailingTokens.Substring(0, trailingTokens.IndexOf(".")), out fileIndex);
                    sFile.FileOrder = fileIndex;
                    mergeList.Add(sFile);
                }

                // sort by the file-part number to ensure we merge back in the correct order
                var mergeOrder = mergeList.OrderBy(s => s.FileOrder).ToList();

                using (FileStream FS = new FileStream(baseFileName, FileMode.Create))
                {
                    // merge each file chunk back into one contiguous file stream
                    foreach (var chunk in mergeOrder)
                    {
                        try
                        {
                            using (FileStream fileChunk = new FileStream(chunk.FileName, FileMode.Open))
                            {
                                fileChunk.CopyTo(FS);
                            }
                            System.IO.File.Delete(chunk.FileName);
                        }
                        catch (IOException ex)
                        {
                            // handle
                        }
                    }
                }
                rslt = true;
                // unlock the file from singleton
                //MergeFileManager.Instance.RemoveFile(baseFileName);
                //}
            }
            return rslt;
        }

        [HttpPost("UploadCachedChunk")]
        public IActionResult UploadCachedChunk(IFormFile fileBase)
        {
            foreach (var file in Request.Form.Files)
            {
                if (file != null && file.Length > 0)
                {
                    try
                    {
                        // take the input stream, and save it to a temp folder using  
                        // the original file.part name posted  
                        var stream = file.OpenReadStream();
                        var fileName = Path.GetFileName(file.FileName);

                        var chunk = StreamToArray(stream);

                        // Set cache options.
                        var cacheEntryOptions = new MemoryCacheEntryOptions()
                            // Keep in cache for this time, reset time if accessed.
                            .SetSlidingExpiration(TimeSpan.FromSeconds(120));

                        // Save data in cache.
                        _cache.Set(fileName, chunk, cacheEntryOptions);
                        MergeCachedFile(fileName);
                    }
                    catch (Exception ex)
                    {

                        throw;
                    }                
                }
            }
            return Ok();
        }

        private bool MergeCachedFile(string FileName)
        {
            bool rslt = false;
            // parse out the different tokens from the filename according to the convention
            string partToken = ".part_";

            string baseFileName = FileName.Substring(0, FileName.IndexOf(partToken));
            string trailingTokens = FileName.Substring(FileName.IndexOf(partToken) + partToken.Length);

            int fileIndex = 0;
            int fileCount = 0;
            int.TryParse(trailingTokens.Substring(0, trailingTokens.IndexOf(".")), out fileIndex);
            int.TryParse(trailingTokens.Substring(trailingTokens.IndexOf(".") + 1), out fileCount);

            if (fileIndex == fileCount)
            {             
                // add each file located to a list so we can get them into
                // the correct order for rebuilding the file
                List<SortedFile> mergeList = new List<SortedFile>();
                for(int i = 1; i <= fileCount; i++)
                {
                    string chunkName = $"{baseFileName}{partToken}{i}.{fileCount}";
                    byte[] chunk;
                    if (_cache.TryGetValue(chunkName, out chunk))
                    {
                        SortedFile sortedChunk = new SortedFile()
                        {
                            FileName = chunkName,
                            FileOrder = i,
                            Bytes = chunk
                        };
                        mergeList.Add(sortedChunk);
                    }
                    else
                    {
                        // coulnd't find the cached chunk
                        throw new FileNotFoundException();
                    }
                }

                string uploadPath = $"App_Data\\uploads\\{baseFileName}";
                using (FileStream stream = new FileStream(uploadPath, FileMode.Create))
                {
                    // merge each file chunk back into one contiguous file stream
                    foreach (var chunk in mergeList)
                    {
                        try
                        {
                            stream.Write(chunk.Bytes, 0, chunk.Bytes.Length);
                            _cache.Remove(chunk.FileName);
                        }
                        catch (IOException ex)
                        {
                            // handle
                        }
                    }
                }
                rslt = true;
                // unlock the file from singleton
                //MergeFileManager.Instance.RemoveFile(baseFileName);
                //}
            }
            return rslt;
        }

        public static byte[] StreamToArray(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }
}
