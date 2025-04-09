using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Zoolander;

    /// <summary>
    /// Listens to audio and detects when sound is present or when silence returns
    /// </summary>
    public class AudioListener : IDisposable
    {
        // Configure sensitivity parameters
        private const float SILENCE_THRESHOLD = 0.01f; // Adjust this value to change sound detection sensitivity
        private const int SILENCE_DURATION_TO_STOP_MS = 2000; // Trigger silence after 2 seconds without sound

        private WasapiLoopbackCapture? _capture;
        private bool _isSoundDetected = false;
        private int _silenceCounter = 0;
        private int _soundCounter = 0;

        // Event handler for when sound is detected
        public event EventHandler? SoundDetected;

        // Event handler for when silence returns
        public event EventHandler? SilenceDetected;

        public AudioListener()
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

            Console.WriteLine("Starting audio listener...");
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
                Console.WriteLine("Audio listener stopped.");
            }
        }

        /// <summary>
        /// Process audio data to detect sound/silence
        /// </summary>
        public void ProcessAudioData(object? sender, WaveInEventArgs e)
        {
            // Check if the buffer contains sound above threshold
            var hasSound = ContainsSound(e.Buffer, e.BytesRecorded);

            if (hasSound)
            {
                // Reset silence counter and increment sound counter
                _silenceCounter = 0;
                _soundCounter++;

                // If we've detected sound for at least a few consecutive samples, trigger sound event
                if (!_isSoundDetected && _soundCounter > 3)
                {
                    _isSoundDetected = true;
                    SoundDetected?.Invoke(this, EventArgs.Empty);
                }
            }
            else // Silence detected
            {
                // Reset sound counter and increment silence counter
                _soundCounter = 0;
                _silenceCounter++;

                // Calculate milliseconds of silence based on buffer size and format
                var samplesPerMillisecond =
                    (_capture?.WaveFormat.SampleRate ?? 44100) *
                    (_capture?.WaveFormat.Channels ?? 2) /
                    1000;

                var silenceDuration = _silenceCounter * e.BytesRecorded /
                    (2 * samplesPerMillisecond); // 2 bytes per sample for 16 bit

                // If we've had enough consecutive silence samples, trigger silence event
                if (_isSoundDetected && silenceDuration > SILENCE_DURATION_TO_STOP_MS)
                {
                    _isSoundDetected = false;
                    SilenceDetected?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Internal handler for data available events
        /// </summary>
        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            ProcessAudioData(sender, e);
        }

        /// <summary>
        /// Determine if the audio buffer contains sound above the threshold
        /// </summary>
        private bool ContainsSound(byte[] buffer, int bytesRecorded)
        {
            // Convert bytes to 16-bit samples and check amplitude
            for (var i = 0; i < bytesRecorded; i += 2)
            {
                if (i + 1 >= bytesRecorded)
                    break;

                // Safely handle the sample conversion to avoid overflow
                int highByte = buffer[i + 1];
                int lowByte = buffer[i];

                // Combine bytes into a 16-bit sample (protecting against overflow)
                var sample = (highByte << 8) | lowByte;
                if (highByte >= 128)
                    // It's a negative number in two's complement
                    sample = sample - 65536;

                var amplitude = Math.Abs(sample / 32768.0f); // Normalize to 0.0-1.0 range

                if (amplitude > SILENCE_THRESHOLD)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Internal handler for when recording is stopped
        /// </summary>
        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
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