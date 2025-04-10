using System;
using System.IO;

namespace ConsoleApp
{
    /// <summary>
    /// Manages audio files, directories, and file naming
    /// </summary>
    public class FileManager
    {
        private readonly object _fileOperationLock = new object();

        /// <summary>
        /// Creates the output directory for audio recordings
        /// </summary>
        /// <returns>The path to the output directory</returns>
        public string CreateOutputDirectory()
        {
            // Create output directory with date
            var baseDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "AudioRecordings",
                DateTime.Now.ToString("yyyy-MM-dd")
            );

            if (!Directory.Exists(baseDirectory))
                Directory.CreateDirectory(baseDirectory);

            return baseDirectory;
        }

        /// <summary>
        /// Creates a new file path for an audio recording
        /// </summary>
        /// <returns>Full path to the new audio file</returns>
        public string CreateNewAudioFilePath()
        {
            var outputDirectory = CreateOutputDirectory();
            var timestamp = DateTime.Now.ToString("HH-mm-ss");
            var fileName = $"recording_{timestamp}.mp3";
            return Path.Combine(outputDirectory, fileName);
        }

        /// <summary>
        /// Gets the total size of all recordings for today
        /// </summary>
        /// <returns>Total size in bytes</returns>
        public long GetTodaysTotalRecordingSize()
        {
            var outputDirectory = CreateOutputDirectory();
            if (!Directory.Exists(outputDirectory))
                return 0;

            long totalSize = 0;
            var files = Directory.GetFiles(outputDirectory, "*.mp3");
            
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                totalSize += fileInfo.Length;
            }

            return totalSize;
        }

        /// <summary>
        /// Gets the number of recordings made today
        /// </summary>
        /// <returns>Number of recordings</returns>
        public int GetTodaysRecordingCount()
        {
            var outputDirectory = CreateOutputDirectory();
            if (!Directory.Exists(outputDirectory))
                return 0;

            return Directory.GetFiles(outputDirectory, "*.mp3").Length;
        }

        /// <summary>
        /// Deletes recordings older than the specified number of days
        /// </summary>
        /// <param name="daysToKeep">Number of days to keep recordings for</param>
        public void CleanupOldRecordings(int daysToKeep = 7)
        {
            try
            {
                var baseDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "AudioRecordings"
                );

                if (!Directory.Exists(baseDirectory))
                    return;

                // Get all date-based subdirectories
                var directories = Directory.GetDirectories(baseDirectory);
                
                foreach (var directory in directories)
                {
                    // Try to parse the directory name as a date
                    if (DateTime.TryParse(Path.GetFileName(directory), out var dirDate))
                    {
                        // If the directory is older than the cutoff date, delete it
                        if (dirDate < DateTime.Now.AddDays(-daysToKeep))
                        {
                            lock (_fileOperationLock)
                            {
                                Console.WriteLine($"Cleaning up old recordings from {dirDate:yyyy-MM-dd}");
                                Directory.Delete(directory, true);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cleaning up old recordings: {ex.Message}");
            }
        }
    }
}