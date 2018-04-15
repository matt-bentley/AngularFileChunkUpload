var app = angular.module('plunker', ['ngMaterial', 'ngMessages']);

app.controller('MainCtrl', function ($scope, $timeout, $http) {

    $scope.loadingInfo = "";

    $scope.uploadFile = function () {
        $scope.loadingInfo = "Starting upload...";
        $scope.loadingPercent = 0;
        var fileUploadElem = document.getElementById("uploadFile");
        var file = fileUploadElem.files[0];
        $scope.fileSize = Math.round(file.size / 1000000);
        //UploadFileChunk(file, file.name);
        UploadFile(file);
    }

    function UploadFile(targetFile) {
        // create array to store the buffer chunks  
        var FileChunk = [];
        // the file object itself that we will work with  
        var file = targetFile;
        // set up other initial vars  
        var maxChunkSize = 1;
        var BufferChunkSize = maxChunkSize * (1024 * 1024);
        var ReadBuffer_Size = 1024;
        var FileStreamPos = 0;
        // set the initial chunk length  
        var EndPos = BufferChunkSize;
        var Size = file.size;

        // add to the FileChunk array until we get to the end of the file  
        while (FileStreamPos < Size) {
            // "slice" the file from the starting position/offset, to  the required length  
            FileChunk.push(file.slice(FileStreamPos, EndPos));
            FileStreamPos = EndPos; // jump by the amount read  
            EndPos = FileStreamPos + BufferChunkSize; // set next chunk length  
        }
        // get total number of "files" we will be sending  
        var TotalParts = FileChunk.length;
        var PartCount = 0;
        // loop through, pulling the first item from the array each time and sending it  
        UploadFileChunks(FileChunk, file.name, 0, TotalParts);
        //var i = 0;
        //while (chunk = FileChunk.shift()) {
        //    PartCount++;
        //    // file name convention  
        //    var FilePartName = file.name + ".part_" + PartCount + "." + TotalParts;
        //    // send the file  

        //    if (i == 0) {
        //        UploadFileChunk(chunk, FilePartName);
        //    }
        //    i++;
        //}
    }

    function UploadFileChunks(chunks, fileName, i, total) {

        if (i == total) {
            $timeout(function () {
                $scope.loadingInfo = "Finished upoading " + fileName;
            });
            return;
        }

        var chunkName = fileName + ".part_" + (i + 1) + "." + total;

        $scope.loadingInfo = "Uploading " + chunkName;

        var data = new FormData();
        data.append('file', chunks[i], chunkName);

        $.ajax({
            url: "api/Files/UploadChunk",
            type: 'POST',
            data: data,
            processData: false,  // tell jQuery not to process the data
            contentType: false,  // tell jQuery not to set contentType
            success: function (result) {
                $timeout(function () {
                    $scope.loadingInfo = "Uploaded " + chunkName;
                    $scope.loadingPercent = (100 * (i + 1)) / total;
                });
                i++;
                UploadFileChunks(chunks, fileName, i, total);
            },
            error: function (jqXHR) {
                // send clean up request
                $scope.loadingInfo = "Error uploading " + chunkName;
            }
        });
    }

    function UploadFileChunk(Chunk, FileName) {
        $scope.loadingInfo = "Uploading " + FileName;

        var data = new FormData();
        data.append('file', Chunk, FileName);

        $.ajax({
            url: "api/Files/UploadChunk",
            type: 'POST',
            data: data,
            processData: false,  // tell jQuery not to process the data
            contentType: false,  // tell jQuery not to set contentType
            success: function (result) {
                $scope.loadingInfo = "Sucessfully uploaded " + FileName;
            },
            error: function (jqXHR) {
                $scope.loadingInfo = "Error uploading " + FileName;
            }
        });
    }

});