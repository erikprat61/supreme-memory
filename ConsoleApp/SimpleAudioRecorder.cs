using NAudio.Wave;
using NAudio.Lame;
using NAudio.CoreAudioApi;
using System;
using System.IO;
using System.Threading;

namespace ConsoleApp
{
    /// <summary>
    /// A simplified audio recorder that uses a more straightforward approach
    /// </summary>
    public class SimpleAudioRecorder : IDisposable
    {
        // Configuration
        private const float SOUND_THRESHOLD = 0.015f;
        private const int SILENCE_DURATION_MS = 3000; // 3 seconds
        private const long MAX_FILE_SIZE_BYTES = 10 * 1024 * 1024; // 10 MB
        
        // Audio capture
        private WasapiLoopbackCapture _capture;
        
        // Recording state
        private bool _isRecording = false;
        private LameMP3FileWriter _writer = null;
        private string _currentFilePath = null;
        private long _currentFileSize = 0;
        private DateTime _lastSoundTime = DateTime.MinValue;
        private DateTime _recordingStartTime = DateTime.MinValue;
        
        // File management
        private string _outputDirectory;
        
        public SimpleAudioRecorder()
        {
            // Create output directory
            _outputDirectory = Path.Combine(
                "Data",
                "AudioRecordings",
                DateTime.Now.ToString("yyyy-MM-dd")
            );
            
            Directory.CreateDirectory(_outputDirectory);
            
            Console.WriteLine($"Recordings will be saved to: {_outputDirectory}");
            
            // Initialize audio capture
            _capture = new WasapiLoopbackCapture();
            _capture.DataAvailable += OnDataAvailable;
        }
        
        /// <summary>
        /// Start the audio recorder
        /// </summary>
        public void Start()
        {
            Console.WriteLine("Starting audio recorder...");
            Console.WriteLine("Waiting for sound...");
            
            _capture.StartRecording();
        }
        
        /// <summary>
        /// Stop the audio recorder
        /// </summary>
        public void Stop()
        {
            _capture.StopRecording();
            
            if (_isRecording)
            {
                StopRecording();
            }
        }
        
        /// <summary>
        /// Create a new audio file path
        /// </summary>
        private string CreateNewAudioFilePath()
        {
            var timestamp = DateTime.Now.ToString("HH-mm-ss");
            var fileName = $"recording_{timestamp}.mp3";
            return Path.Combine(_outputDirectory, fileName);
        }
        
        /// <summary>
        /// Start recording to a new file
        /// </summary>
        private void StartRecording()
        {
            if (_isRecording)
                return;
                
            _recordingStartTime = DateTime.Now;
            _currentFilePath = CreateNewAudioFilePath();
            _currentFileSize = 0;
            
            // Create MP3 writer
            _writer = new LameMP3FileWriter(
                _currentFilePath,
                _capture.WaveFormat,
                LAMEPreset.STANDARD
            );
            
            _isRecording = true;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Sound detected - recording started: {Path.GetFileName(_currentFilePath)}");
        }
        
        /// <summary>
        /// Stop the current recording
        /// </summary>
        private void StopRecording()
        {
            if (!_isRecording)
                return;
                
            _isRecording = false;
            
            // Close the writer
            _writer.Flush();
            _writer.Dispose();
            _writer = null;
            
            TimeSpan duration = DateTime.Now - _recordingStartTime;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Recording stopped. Duration: {duration.TotalSeconds:F1} seconds");
            
            // Check if file is too small or too short
            if (File.Exists(_currentFilePath))
            {
                var fileInfo = new FileInfo(_currentFilePath);
                
                // Delete if either:
                // 1. File is too small (less than 10 KB), or
                // 2. Recording duration is 3 seconds or less
                if (fileInfo.Length < 10 * 1024 || duration.TotalSeconds <= 10.0)
                {
                    Console.WriteLine($"Deleting recording that was too small or too short: {Path.GetFileName(_currentFilePath)}");
                    File.Delete(_currentFilePath);
                }
                else
                {
                    Console.WriteLine($"Recording saved: {Path.GetFileName(_currentFilePath)} ({fileInfo.Length / 1024} KB)");
                }
            }
            
            Console.WriteLine("Waiting for sound...");
        }
        
        /// <summary>
        /// Process audio data and handle recording
        /// </summary>
        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            // Check if the buffer has sound
            bool hasSound = DetectSound(e.Buffer, e.BytesRecorded);
            
            if (hasSound)
            {
                // Update last sound time
                _lastSoundTime = DateTime.Now;
                
                // Start recording if not already recording
                if (!_isRecording)
                {
                    StartRecording();
                }
            }
            
            // If we're recording
            if (_isRecording)
            {
                // Check if we've been silent for long enough to stop
                TimeSpan silenceDuration = DateTime.Now - _lastSoundTime;
                if (!hasSound && silenceDuration.TotalMilliseconds >= SILENCE_DURATION_MS)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Silence detected for {silenceDuration.TotalSeconds:F1} seconds");
                    StopRecording();
                    return;
                }
                
                // Check if we'd exceed maximum file size
                if (_currentFileSize + e.BytesRecorded > MAX_FILE_SIZE_BYTES)
                {
                    Console.WriteLine("Maximum file size reached (10 MB)");
                    StopRecording();
                    StartRecording(); // Start a new recording
                    
                    // Write the current buffer to the new file
                    _writer.Write(e.Buffer, 0, e.BytesRecorded);
                    _currentFileSize += e.BytesRecorded;
                    return;
                }
                
                // Write audio data to file
                _writer.Write(e.Buffer, 0, e.BytesRecorded);
                _currentFileSize += e.BytesRecorded;
            }
        }
        
        /// <summary>
        /// Detect if audio buffer contains sound above threshold
        /// </summary>
        private bool DetectSound(byte[] buffer, int bytesRecorded)
        {
            int step = Math.Max(1, bytesRecorded / 100);
            int soundSamples = 0;
            int totalSamples = 0;
            
            for (int i = 0; i < bytesRecorded - 1; i += step)
            {
                if (i + 1 >= bytesRecorded)
                    break;
                    
                // Convert bytes to sample
                int highByte = buffer[i + 1];
                int lowByte = buffer[i];
                
                // Combine bytes into a 16-bit sample
                var sample = (highByte << 8) | lowByte;
                if (highByte >= 128)
                    sample = sample - 65536;
                    
                var amplitude = Math.Abs(sample / 32768.0f);
                
                totalSamples++;
                if (amplitude > SOUND_THRESHOLD)
                    soundSamples++;
            }
            
            // Consider it as sound if at least 3% of samples are above threshold
            return totalSamples > 0 && (float)soundSamples / totalSamples > 0.03f;
        }
        
        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            Stop();
            
            if (_capture != null)
            {
                _capture.Dispose();
                _capture = null;
            }
            
            if (_writer != null)
            {
                _writer.Dispose();
                _writer = null;
            }
        }
    }
}