using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;

namespace ConsoleApp
{
    /// <summary>
    /// Detects sound and silence in audio streams
    /// </summary>
    public class SoundDetector : IDisposable
    {
        // Configure sensitivity parameters
        private const float SILENCE_THRESHOLD = 0.01f; // Sound detection threshold 
        private const float SOUND_THRESHOLD = 0.015f;  // Higher threshold for detecting sound (hysteresis)
        private const int SILENCE_DURATION_TO_STOP_MS = 3000; // Silence duration before stopping (3 seconds)
        private const int MIN_SOUND_SAMPLES = 5; // Increased minimum consecutive sound samples
        private const int DEBOUNCE_MS = 2000; // Increased debounce period to prevent rapid on/off
        
        private WasapiLoopbackCapture _capture;
        private bool _isSoundDetected = false;
        private readonly Queue<bool> _soundHistory = new Queue<bool>();
        private readonly object _historyLock = new object(); // Lock object for thread safety
        private int _consecutiveSilentSamples = 0;
        private int _consecutiveSoundSamples = 0;
        private DateTime _lastSoundTime = DateTime.MinValue;
        private DateTime _lastSilenceTime = DateTime.MinValue;
        private DateTime _lastEventTime = DateTime.MinValue;

        // Event handler for when sound is detected
        public event EventHandler SoundDetected;

        // Event handler for when silence returns
        public event EventHandler SilenceDetected;

        public SoundDetector()
        {
            _capture = new WasapiLoopbackCapture();
            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;
        }

        /// <summary>
        /// Start listening for audio
        /// </summary>
        public void StartListening()
        {
            if (_capture == null)
            {
                _capture = new WasapiLoopbackCapture();
                _capture.DataAvailable += OnDataAvailable;
                _capture.RecordingStopped += OnRecordingStopped;
            }

            Console.WriteLine("Starting sound detector...");
            _capture.StartRecording();
        }

        /// <summary>
        /// Stop listening for audio
        /// </summary>
        public void StopListening()
        {
            if (_capture != null && _capture.CaptureState == CaptureState.Capturing)
            {
                _capture.StopRecording();
                Console.WriteLine("Sound detector stopped.");
            }
        }

        /// <summary>
        /// Process audio data to detect sound/silence
        /// </summary>
        public void ProcessAudioData(object sender, WaveInEventArgs e)
        {
            // Don't process if the buffer is empty
            if (e.BytesRecorded <= 0)
                return;

            // Check if the buffer contains sound above threshold
            var hasSound = ContainsSound(e.Buffer, e.BytesRecorded);

            // Add to sound history for better detection
            lock (_historyLock) // Use dedicated lock object
            {
                _soundHistory.Enqueue(hasSound);
                if (_soundHistory.Count > 20) // Keep more history for stability (20 samples)
                    _soundHistory.Dequeue();
                
                // Calculate sound ratio in recent history
                int soundCount = 0;
                // Make a copy of the queue for safe enumeration
                bool[] historyCopy = _soundHistory.ToArray();
                
                foreach (var sound in historyCopy)
                {
                    if (sound) soundCount++;
                }
                float soundRatio = historyCopy.Length > 0 ? (float)soundCount / historyCopy.Length : 0;
            
                // Update counters based on current sound state
                if (hasSound)
                {
                    _lastSoundTime = DateTime.Now;
                    _consecutiveSilentSamples = 0;
                    _consecutiveSoundSamples++;

                    // Detect sound after enough consecutive sound samples
                    // More strict conditions for triggering sound detection
                    if (!_isSoundDetected && 
                        _consecutiveSoundSamples >= MIN_SOUND_SAMPLES && 
                        soundRatio > 0.4f && // Increased ratio threshold (40% of recent samples have sound)
                        (DateTime.Now - _lastEventTime).TotalMilliseconds > DEBOUNCE_MS)
                    {
                        _isSoundDetected = true;
                        _lastEventTime = DateTime.Now;
                        SoundDetected?.Invoke(this, EventArgs.Empty);
                    }
                }
                else // Silence detected
                {
                    _lastSilenceTime = DateTime.Now;
                    // Don't reset sound counter immediately for better stability
                    _consecutiveSilentSamples++;

                    // Calculate silence duration directly from last sound time
                    var silenceDuration = (DateTime.Now - _lastSoundTime).TotalMilliseconds;

                    // More strict conditions for triggering silence detection
                    if (_isSoundDetected && 
                        silenceDuration >= SILENCE_DURATION_TO_STOP_MS &&
                        soundRatio < 0.1f && // Low sound ratio in recent history
                        _consecutiveSilentSamples > 10 && // Added minimum consecutive silent samples
                        (DateTime.Now - _lastEventTime).TotalMilliseconds > DEBOUNCE_MS)
                    {
                        _isSoundDetected = false;
                        _consecutiveSoundSamples = 0; // Now reset sound counter
                        _lastEventTime = DateTime.Now;
                        SilenceDetected?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        /// <summary>
        /// Internal handler for data available events
        /// </summary>
        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            ProcessAudioData(sender, e);
        }

        /// <summary>
        /// Determine if the audio buffer contains sound above the threshold
        /// </summary>
        private bool ContainsSound(byte[] buffer, int bytesRecorded)
        {
            // Analyze more samples for better accuracy
            int step = Math.Max(1, bytesRecorded / 200); // Analyze up to 200 samples throughout the buffer
            int samplesAnalyzed = 0;
            int soundSamples = 0;
            float maxAmplitude = 0;

            for (var i = 0; i < bytesRecorded - 1; i += step)
            {
                if (i + 1 >= bytesRecorded)
                    break;

                // Sample conversion to avoid overflow
                int highByte = buffer[i + 1];
                int lowByte = buffer[i];

                // Combine bytes into a 16-bit sample
                var sample = (highByte << 8) | lowByte;
                if (highByte >= 128)
                    sample = sample - 65536;

                var amplitude = Math.Abs(sample / 32768.0f); // Normalize to 0.0-1.0 range
                maxAmplitude = Math.Max(maxAmplitude, amplitude);

                samplesAnalyzed++;
                
                // Use different thresholds based on current state - higher threshold to detect sound, 
                // lower threshold to maintain sound state (hysteresis)
                float threshold = _isSoundDetected ? SILENCE_THRESHOLD : SOUND_THRESHOLD;
                
                if (amplitude > threshold)
                    soundSamples++;
            }

            // Return true if either:
            // 1. At least 3% of analyzed samples contain sound, or
            // 2. We detect a significant peak (max amplitude is quite high)
            return (samplesAnalyzed > 0 && (float)soundSamples / samplesAnalyzed > 0.03f) || 
                   (maxAmplitude > SOUND_THRESHOLD * 3);
        }

        /// <summary>
        /// Internal handler for when recording is stopped
        /// </summary>
        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
                Console.WriteLine($"Recording error: {e.Exception.Message}");
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            if (_capture != null)
            {
                if (_capture.CaptureState == CaptureState.Capturing)
                    _capture.StopRecording();

                _capture.Dispose();
                _capture = null;
            }
        }
    }
}