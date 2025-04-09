using System.Text;
using NAudio.Wave;
using Whisper.net;

namespace Zoolander;

/// <summary>
/// Service for real-time audio transcription using Whisper.net
/// </summary>
public class RealTimeTranscriptionService : IDisposable
{
    private readonly WhisperFactory _whisperFactory;
    private readonly List<byte[]> _audioBuffers = new();
    private readonly object _bufferLock = new();
    private readonly WaveFormat _whisperFormat = new(16000, 16, 1); // 16kHz, 16-bit, mono
    private WhisperProcessor? _whisperProcessor;
    private bool _isProcessing = false;
    private int _bufferSizeMs = 5000; // Process 5 seconds of audio at a time
    private DateTime _lastProcessTime = DateTime.MinValue;
    
    // Event that fires when new transcription text is available
    public event EventHandler<string>? TranscriptionReceived;

    /// <summary>
    /// Creates a new real-time transcription service
    /// </summary>
    /// <param name="modelPath">Path to the Whisper model file</param>
    /// <param name="bufferSizeMs">Size of audio buffer in milliseconds (default is 5000)</param>
    public RealTimeTranscriptionService(string modelPath, int bufferSizeMs = 5000)
    {
        _whisperFactory = WhisperFactory.FromPath(modelPath);
        _whisperProcessor = _whisperFactory.CreateBuilder()
            .WithLanguage("auto")
            .Build();
        _bufferSizeMs = bufferSizeMs;
    }

    /// <summary>
    /// Adds audio data to the buffer for processing
    /// </summary>
    /// <param name="sourceBuffer">Audio buffer in the source format</param>
    /// <param name="bytesRecorded">Number of bytes in the buffer</param>
    /// <param name="sourceFormat">Format of the source audio</param>
    public void AddAudioData(byte[] sourceBuffer, int bytesRecorded, WaveFormat sourceFormat)
    {
        // If the source format doesn't match Whisper's expected format, convert it
        if (!sourceFormat.Equals(_whisperFormat))
        {
            // Create a temporary buffered provider with the source format
            var bufferedProvider = new BufferedWaveProvider(sourceFormat)
            {
                DiscardOnBufferOverflow = true
            };
            bufferedProvider.AddSamples(sourceBuffer, 0, bytesRecorded);
            
            // Create a converter to convert to Whisper format
            using var converter = new MediaFoundationResampler(bufferedProvider, _whisperFormat)
            {
                ResamplerQuality = 60 // High quality
            };
            
            // Calculate an appropriate buffer size for the converted audio
            var sourceBytes = sourceFormat.AverageBytesPerSecond;
            var targetBytes = _whisperFormat.AverageBytesPerSecond;
            var ratio = (float)targetBytes / sourceBytes;
            var convertedSize = (int)(bytesRecorded * ratio);
            
            // Read the converted data
            var convertedBuffer = new byte[convertedSize];
            var bytesRead = converter.Read(convertedBuffer, 0, convertedSize);
            
            // If we read any bytes, add them to our audio buffer
            if (bytesRead > 0)
            {
                var trimmedBuffer = new byte[bytesRead];
                Array.Copy(convertedBuffer, trimmedBuffer, bytesRead);
                
                lock (_bufferLock)
                {
                    _audioBuffers.Add(trimmedBuffer);
                }
            }
        }
        else
        {
            // Audio is already in the correct format, add it directly
            var buffer = new byte[bytesRecorded];
            Array.Copy(sourceBuffer, buffer, bytesRecorded);
            
            lock (_bufferLock)
            {
                _audioBuffers.Add(buffer);
            }
        }
        
        // Check if we should process the buffer
        CheckAndProcessBuffer();
    }
    
    /// <summary>
    /// Checks if enough audio data has been collected and processes it
    /// </summary>
    private async void CheckAndProcessBuffer()
    {
        // Don't start a new processing task if one is already running
        if (_isProcessing)
            return;
            
        // Only process if enough time has passed since the last processing
        var timeSinceLastProcess = DateTime.Now - _lastProcessTime;
        if (timeSinceLastProcess.TotalMilliseconds < _bufferSizeMs / 2)
            return;
            
        byte[] combinedBuffer;
        lock (_bufferLock)
        {
            // If not enough audio data has been collected, wait for more
            if (_audioBuffers.Count == 0)
                return;
                
            // Calculate total buffer size
            int totalBytes = _audioBuffers.Sum(b => b.Length);
            
            // Calculate required buffer size for the time window
            var requiredBytes = _whisperFormat.AverageBytesPerSecond * _bufferSizeMs / 1000;
            
            // If not enough data yet, wait for more
            if (totalBytes < requiredBytes)
                return;
                
            // Combine all buffers
            combinedBuffer = new byte[totalBytes];
            int offset = 0;
            foreach (var buffer in _audioBuffers)
            {
                Array.Copy(buffer, 0, combinedBuffer, offset, buffer.Length);
                offset += buffer.Length;
            }
            
            // Clear the buffer queue after processing
            _audioBuffers.Clear();
        }
        
        _isProcessing = true;
        _lastProcessTime = DateTime.Now;
        
        try
        {
            // Create an in-memory stream with the audio data
            using var audioStream = new MemoryStream(combinedBuffer);
            
            // Process the audio with Whisper
            var transcription = await TranscribeAudioStreamAsync(audioStream);
            
            // Notify listeners if transcription is not empty
            if (!string.IsNullOrWhiteSpace(transcription))
            {
                TranscriptionReceived?.Invoke(this, transcription);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in real-time transcription: {ex.Message}");
        }
        finally
        {
            _isProcessing = false;
        }
    }
    
    /// <summary>
    /// Transcribes an audio stream
    /// </summary>
    /// <param name="audioStream">Stream containing audio data</param>
    /// <returns>Transcription text</returns>
    private async Task<string> TranscribeAudioStreamAsync(Stream audioStream)
    {
        if (_whisperProcessor == null)
        {
            _whisperProcessor = _whisperFactory.CreateBuilder()
                .WithLanguage("auto")
                .Build();
        }
        
        var segmentsResult = _whisperProcessor.ProcessAsync(audioStream);
        
        var transcription = new StringBuilder();
        await foreach (var segment in segmentsResult)
        {
            transcription.Append(segment.Text + " ");
        }
        
        return transcription.ToString().Trim();
    }
    
    /// <summary>
    /// Forces processing of the current audio buffer
    /// </summary>
    public void ProcessCurrentBuffer()
    {
        CheckAndProcessBuffer();
    }
    
    /// <summary>
    /// Disposes resources used by the transcription service
    /// </summary>
    public void Dispose()
    {
        _whisperProcessor?.Dispose();
        _whisperFactory?.Dispose();
    }
}