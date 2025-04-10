using System;
using System.Threading;

namespace ConsoleApp
{
    public class Program
    {
        private static bool _running = true;
        private static readonly ManualResetEvent _exitEvent = new ManualResetEvent(false);

        public static void Main(string[] args)
        {
            Console.WriteLine("Simple Audio Recording Service Starting...");
            Console.WriteLine("This app records your computer's audio output to MP3 files.");
            Console.WriteLine("Recording starts automatically when sound is detected.");
            Console.WriteLine("Recording stops after 3 seconds of silence.");
            Console.WriteLine("Each recording file has a maximum size of 10 MB.");
            Console.WriteLine("Press Ctrl+C to exit the application");
            Console.WriteLine();

            // Handle console shutdown
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true; // Prevent the process from terminating immediately
                _running = false;
                _exitEvent.Set();
                Console.WriteLine("Shutdown requested, finishing processing...");
            };

            // Initialize and start the audio recorder
            using var audioRecorder = new SimpleAudioRecorder();
            audioRecorder.Start();

            // Simple status update loop
            var statusTimer = new Timer(_ =>
            {
                if (_running)
                {
                    var recordingCount = CountRecordings();
                    var totalSizeMB = GetTotalRecordingSize() / (1024.0 * 1024.0);
                    Console.WriteLine($"Status: {recordingCount} recordings, {totalSizeMB:F2} MB total");
                }
            }, null, 60000, 60000); // Update every minute

            // Wait for exit signal
            _exitEvent.WaitOne();
            
            // Clean up
            statusTimer.Dispose();
            Console.WriteLine("Application shutting down.");
        }

        // Count number of recordings
        private static int CountRecordings()
        {
            var outputDirectory = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "AudioRecordings",
                DateTime.Now.ToString("yyyy-MM-dd")
            );
            
            if (!System.IO.Directory.Exists(outputDirectory))
                return 0;
                
            return System.IO.Directory.GetFiles(outputDirectory, "*.mp3").Length;
        }

        // Get total size of recordings
        private static long GetTotalRecordingSize()
        {
            var outputDirectory = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "AudioRecordings",
                DateTime.Now.ToString("yyyy-MM-dd")
            );
            
            if (!System.IO.Directory.Exists(outputDirectory))
                return 0;
                
            long totalSize = 0;
            var files = System.IO.Directory.GetFiles(outputDirectory, "*.mp3");
            
            foreach (var file in files)
            {
                var fileInfo = new System.IO.FileInfo(file);
                totalSize += fileInfo.Length;
            }
            
            return totalSize;
        }
    }
}