using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace ConsoleApp;

public class AudioMonitor : IDisposable
{
    // Configure sensitivity and timing parameters
    private const float SILENCE_THRESHOLD = 0.01f; // Adjust this value to change sound detection sensitivity
    private const int SILENCE_DURATION_TO_STOP_MS = 10 * 1000; // Stop recording after 10 seconds of silence
    private const int MINIMUM_RECORDING_DURATION_MS = 3 * 1000; // Minimum recording length to save (3 seconds)
    private const long MAX_FILE_SIZE_BYTES = 10 * 1024 * 1024; // Maximum file size (10 MB)

    private readonly string _outputDirectory;
    private readonly TranscriptionService _transcriptionService;
    private readonly BufferedWaveProvider? _bufferedWaveProvider;
    private WasapiLoopbackCapture? _capture;
    private byte[] _convertBuffer = new byte[4096]; // Buffer for converted audio
    private readonly IWaveProvider? _convertedProvider;
    private readonly CancellationTokenSource _cts;
    private string? _currentFilePath;
    private long _currentFileSize; // Track current file size
    private bool _isRecording;
    private int _partNumber = 1; // For tracking file parts when splitting
    private readonly List<string> _recordingParts = new(); // Keep track of all parts of a recording
    private DateTime _recordingStartTime;
    private int _silenceCounter;
    private readonly WaveFormat _whisperFormat = new(16000, 16, 1); // 16kHz, 16-bit, mono
    private WaveFileWriter? _writer;

    public AudioMonitor(string outputDirectory, TranscriptionService transcriptionService)
    {
        _outputDirectory = outputDirectory;
        _transcriptionService = transcriptionService;
        _capture = new WasapiLoopbackCapture();
        _cts = new CancellationTokenSource();

        // Create a buffered provider with the capture format
        _bufferedWaveProvider = new BufferedWaveProvider(_capture.WaveFormat)
        {
            DiscardOnBufferOverflow = true
        };

        // Create the converter from capture format to Whisper format
        _convertedProvider = new MediaFoundationResampler(_bufferedWaveProvider, _whisperFormat)
        {
            ResamplerQuality = 60 // High quality
        };

        _capture.DataAvailable += CaptureOnDataAvailable;
        _capture.RecordingStopped += CaptureOnRecordingStopped;
    }

    public void Dispose()
    {
        _cts.Cancel();

        if (_isRecording) StopRecording();

        if (_convertedProvider is IDisposable disposableProvider) disposableProvider.Dispose();

        if (_capture != null)
        {
            // Check if capture is recording
            if (_capture.CaptureState == CaptureState.Capturing) _capture.StopRecording();

            _capture.Dispose();
            _capture = null;
        }

        if (_writer != null)
        {
            _writer.Dispose();
            _writer = null;
        }
    }

    public async Task StartMonitoring()
    {
        _capture?.StartRecording();

        // Keep the method running until cancellation
        try
        {
            await Task.Delay(Timeout.Infinite, _cts.Token);
        }
        catch (TaskCanceledException)
        {
            // Expected on cancellation
        }
    }

    private void CaptureOnDataAvailable(object? sender, WaveInEventArgs e)
    {
        // Add the data to the buffer
        if (_bufferedWaveProvider != null) _bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);

        // Check if the buffer contains sound above threshold
        var hasSound = ContainsSound(e.Buffer, e.BytesRecorded);

        if (hasSound)
        {
            _silenceCounter = 0;

            if (!_isRecording) StartRecording();

            // Write data to file if we're recording (in the correct format)
            if (_isRecording && _writer != null && _convertedProvider != null) WriteAudioDataToFile(e.BytesRecorded);
        }
        else // Silence detected
        {
            _silenceCounter++;

            // If we have silence for enough time and we're recording, stop
            var samplesPerMillisecond =
                (_capture?.WaveFormat.SampleRate ?? 44100)
                * (_capture?.WaveFormat.Channels ?? 2)
                / 1000;
            var silenceDuration =
                _silenceCounter * e.BytesRecorded / (2 * samplesPerMillisecond); // 2 bytes per sample for 16 bit

            if (_isRecording && silenceDuration > SILENCE_DURATION_TO_STOP_MS) StopRecording();

            // Continue writing to file during silence when recording
            if (_isRecording && _writer != null && _convertedProvider != null) WriteAudioDataToFile(e.BytesRecorded);
        }
    }

    private void WriteAudioDataToFile(int originalBytesRecorded)
    {
        // Calculate an appropriate buffer size - convert from original format to 16kHz mono
        var originalBytesPerSample =
            _capture?.WaveFormat.BitsPerSample / 8 * _capture?.WaveFormat.Channels ?? 4;
        var whisperBytesPerSample = _whisperFormat.BitsPerSample / 8 * _whisperFormat.Channels;

        // Adjust buffer size based on ratio between formats
        var conversionRatio =
            (float)_whisperFormat.SampleRate
            / (_capture?.WaveFormat.SampleRate ?? 44100)
            * whisperBytesPerSample
            / originalBytesPerSample;

        var convertBufferSize = (int)(originalBytesRecorded * conversionRatio);

        // Make sure the buffer is large enough
        if (_convertBuffer.Length < convertBufferSize) _convertBuffer = new byte[convertBufferSize];

        var bytesRead = _convertedProvider!.Read(_convertBuffer, 0, convertBufferSize);

        if (bytesRead > 0)
        {
            // Check if adding this data would exceed max file size
            if (_currentFileSize + bytesRead > MAX_FILE_SIZE_BYTES && _writer != null)
            {
                // Close current file and start a new part
                var completedFilePath = _currentFilePath!;
                _writer.Dispose();

                // Save the current part to our list
                _recordingParts.Add(completedFilePath);

                // Start new file part
                _partNumber++;
                _currentFilePath = GetNewRecordingPath(_partNumber);

                Console.WriteLine($"Creating new file part: {Path.GetFileName(_currentFilePath)}");

                // Initialize new writer
                _writer = new WaveFileWriter(_currentFilePath, _whisperFormat);
                _currentFileSize = 0;
            }

            // Write data to current file
            _writer!.Write(_convertBuffer, 0, bytesRead);
            _currentFileSize += bytesRead;
        }
    }

    private string GetNewRecordingPath(int partNumber)
    {
        return Path.Combine(
            _outputDirectory,
            $"{_recordingStartTime:HH-mm-ss}_recording_part{partNumber}.wav"
        );
    }

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

            var amplitude = Math.Abs(sample / 32768.0f); // Normalize to 0.0-1.0 using floating point division

            if (amplitude > SILENCE_THRESHOLD)
                return true;
        }

        return false;
    }

    private void StartRecording()
    {
        _recordingStartTime = DateTime.Now;
        _isRecording = true;
        _partNumber = 1;
        _currentFileSize = 0;
        _recordingParts.Clear();

        Console.WriteLine($"Recording started at {_recordingStartTime:HH:mm:ss}");

        // Create file for recording
        _currentFilePath = Path.Combine(
            _outputDirectory,
            $"{_recordingStartTime:HH-mm-ss}_recording.wav"
        );

        // Initialize the writer with the Whisper format directly
        _writer = new WaveFileWriter(_currentFilePath, _whisperFormat);

        // Clear any buffered audio to start fresh
        if (_bufferedWaveProvider != null) _bufferedWaveProvider.ClearBuffer();
    }

    private void StopRecording()
    {
        if (!_isRecording)
            return;

        _isRecording = false;
        var recordingEndTime = DateTime.Now;
        var duration = recordingEndTime - _recordingStartTime;

        Console.WriteLine(
            $"Recording stopped at {recordingEndTime:HH:mm:ss}, duration: {duration.TotalSeconds:F1} seconds"
        );

        // Close the writer
        if (_writer != null)
        {
            // Add the current file to our parts list
            if (!string.IsNullOrEmpty(_currentFilePath)) _recordingParts.Add(_currentFilePath);

            _writer.Dispose();
            _writer = null;

            // Only process if recording was long enough
            if (
                duration.TotalMilliseconds > MINIMUM_RECORDING_DURATION_MS
                && _recordingParts.Count > 0
            )
                ProcessRecording(recordingEndTime);
            else
                Console.WriteLine("Recording too short, keeping files but not transcribing");

            // Clear the parts list
            _recordingParts.Clear();
        }
    }

    private void ProcessRecording(DateTime recordingEndTime)
    {
        List<string> finalPaths = new List<string>();

        // Always treat recordings as parts, even if there's only one
        for (var i = 0; i < _recordingParts.Count; i++)
        {
            // Always add part suffix regardless of number of parts
            var partSuffix = $"_part{i + 1}";
            var newFileName =
                $"{_recordingStartTime:HH-mm-ss}_to_{recordingEndTime:HH-mm-ss}{partSuffix}.wav";
            var newFilePath = Path.Combine(_outputDirectory, newFileName);

            try
            {
                // Move the audio file
                File.Move(_recordingParts[i], newFilePath);
                finalPaths.Add(newFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error renaming file part {i + 1}: {ex.Message}");
                // If renaming fails, use the original path
                finalPaths.Add(_recordingParts[i]);
            }
        }

        // Process transcriptions - first transcribe individual files
        foreach (var path in finalPaths) Task.Run(() => _transcriptionService.TranscribeAudioFile(path));

        // Then create combined transcription for all recordings (even single files)
        if (finalPaths.Count > 0)
            Task.Run(
                () =>
                    _transcriptionService.ProcessMultiPartRecording(
                        finalPaths,
                        _recordingStartTime,
                        recordingEndTime,
                        _outputDirectory
                    )
            );
    }

    private void CaptureOnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        // Handle any recording stop cleanup
        if (_writer != null)
        {
            _writer.Dispose();
            _writer = null;
        }

        if (e.Exception != null) Console.WriteLine($"Recording error: {e.Exception.Message}");
    }
}