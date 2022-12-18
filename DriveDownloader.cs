using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KRFP
{
    internal class DriveDownloader
    {

        private DriveService _driveService;
        private static DriveDownloader _instance;

        private readonly static string _credentials = "ServiceAccountCredentials.json";

        /// <summary>
        /// Get the current instance of the Drive Downloader
        /// </summary>
        public static DriveDownloader Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new DriveDownloader(_credentials);
                return _instance;
            }
        }

        /// <summary>
        /// Singleton Google Drive Downloader
        /// </summary>
        private DriveDownloader(string credentialsFile)
        {
            _driveService = MakeDriveService(credentialsFile);
        }

        /// <summary>
        /// Extract the File ID from a Google Drive URL
        /// </summary>
        /// <param name="url">URL to extract file ID from</param>
        /// <returns>File ID if found, otherwise empty string</returns>
        private string ExtractFileId(string url)
        {
            string[] splits = url.Split("id=");
            if(splits.Length == 2)
                return splits[1];
            return string.Empty;
        }

        /// <summary>
        /// Make Drive Service from Credentials
        /// </summary>
        /// <param name="credentialFile"></param>
        /// <returns></returns>
        private DriveService MakeDriveService(string credentialFile)
        {
            try
            {
                Stream credentialStream = File.Open(credentialFile, FileMode.Open, FileAccess.Read);
                ServiceAccountCredential serviceCredential = ServiceAccountCredential.FromServiceAccountData(credentialStream);
                GoogleCredential credential = GoogleCredential.FromServiceAccountCredential(serviceCredential).CreateScoped(DriveService.Scope.Drive);

                var service = new DriveService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "Drive API Snippets"
                });
                return service;
            }
            catch (Exception e)
            {
                if (e is AggregateException)
                {
                    Console.WriteLine("Credential Not found");
                }
                else
                {
                    throw e;
                }
            }
            return null;
        }

        /// <summary>
        /// Request a File via the Google Drive URL.
        /// </summary>
        /// <param name="url">URL to download from.</param>
        /// <returns>Filepath to saved file, otherwise empty String.</returns>
        public string RequestFile(string url)
        {
            // Extract File ID from url and save
            string id = ExtractFileId(url);
            if (id == string.Empty)
                return string.Empty;

            // Make Request
            FilesResource.GetRequest request = _driveService.Files.Get(id);

            // Find the File Name
            var file = request.Execute();
            string name = file.Name;
            if (!name.EndsWith(".ydk"))
                return string.Empty;

            // Resolve Duplicate File Names
            name = "Decks\\" + name.Trim();
            if(File.Exists(name))
            {
                Debug.WriteLine("Resolving duplicate file name: \"" + name + "\".");
                string originalName = name.Remove(name.Length - 4, 4).Trim();
                int i = 1;
                for (; File.Exists(originalName + "(" + i + ").ydk"); i++) ;
                name = originalName + "(" + i + ").ydk";
            }
            
            // Stream to store download
            MemoryStream stream = new MemoryStream();

            /*
             * Add a handler which will be notified on progress changes.
             * It will notify on each chunk download and when the
             * download is completed or failed.
             */
            request.MediaDownloader.ProgressChanged += progress =>
            {
                switch (progress.Status)
                {
                    case DownloadStatus.Completed:
                    {
                        Debug.WriteLine("Download complete.");
                        SaveStream(stream, name);
                        break;
                    }
                    case DownloadStatus.Failed:
                    {
                        Console.WriteLine("Download failed.");
                        break;
                    }
                }
            };

            // Download File
            request.DownloadWithStatus(stream);

            // If file does not exist due to failed download return empty, otherwise file name
            return File.Exists(name) ? name : string.Empty;
        }

        /// <summary>
        /// Save MemoryStream to a file
        /// </summary>
        /// <param name="stream">Stream to save to Filepath</param>
        /// <param name="saveTo">Filepath to save Stream to</param>
        private void SaveStream(MemoryStream stream, string saveTo)
        {
            using (FileStream file = new FileStream(saveTo, FileMode.Create, FileAccess.Write))
            {
                stream.WriteTo(file);
            }
        }
    }
}