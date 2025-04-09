using System.Collections.Concurrent;
using NAudio.Wave;
using Whisper.net;

namespace Zoolander;

/// <summary>
/// Service for buffered real-time audio transcription using Whisper.net
/// </summary>
public class BufferedTranscriptionService : IDisposable
{
    // Fixed audio chunk size in milliseconds - represents the ideal chunk size for transcription
    private const int CHUNK_SIZE_MS = 10000; // 10 seconds per chunk (increased from 3)

    private readonly WhisperFactory _whisperFactory;
    private readonly ConcurrentQueue<byte[]> _audioChunks = new();
    private readonly WaveFormat _whisperFormat = new(8000, 16, 1); // 8kHz, 16-bit, mono (reduced from 16kHz)
    private readonly MemoryStream _currentChunkStream;
    private long _currentChunkBytes = 0;
    private readonly long _targetChunkBytes;
    private readonly Task _processingTask;
    private readonly CancellationTokenSource _cts = new();
    private DateTime _lastProcessingTime = DateTime.MinValue;
    private const float SILENCE_THRESHOLD = 0.01f;
    private const float VOICE_THRESHOLD = 0.025f; // Higher threshold for voice detection
    private const float SILENCE_PERCENTAGE_THRESHOLD = 0.85f; // Skip chunks with more than 85% silence

    // Event that fires when new transcription text is available
    public event EventHandler<string>? TranscriptionReceived;

    /// <summary>
    /// Creates a new buffered transcription service
    /// </summary>
    /// <param name="modelPath">Path to the Whisper model file</param>
    public BufferedTranscriptionService(string modelPath)
    {
        _whisperFactory = WhisperFactory.FromPath(modelPath);

        // Calculate target chunk size in bytes
        _targetChunkBytes = _whisperFormat.AverageBytesPerSecond * CHUNK_SIZE_MS / 1000;

        // Initialize the memory stream for collecting chunks
        _currentChunkStream = new MemoryStream();

        // Start background processing task
        _processingTask = Task.Run(ProcessAudioChunksAsync, _cts.Token);
    }

    /// <summary>
    /// Adds audio data to the current chunk buffer
    /// </summary>
    /// <param name="sourceBuffer">Audio buffer in the source format</param>
    /// <param name="bytesRecorded">Number of bytes in the buffer</param>
    /// <param name="sourceFormat">Format of the source audio</param>
    public void AddAudioData(byte[] sourceBuffer, int bytesRecorded, WaveFormat sourceFormat)
    {
        // Convert audio format if needed
        byte[] whisperFormatBuffer = ConvertAudioFormat(sourceBuffer, bytesRecorded, sourceFormat);

        // OPTIMIZATION 3: Skip if doesn't contain voice
        if (!ContainsVoice(whisperFormatBuffer))
            return;

        // Add the converted audio to the current chunk
        lock (_currentChunkStream)
        {
            _currentChunkStream.Write(whisperFormatBuffer, 0, whisperFormatBuffer.Length);
            _currentChunkBytes += whisperFormatBuffer.Length;

            // If we've reached the target chunk size, enqueue it for processing
            if (_currentChunkBytes >= _targetChunkBytes)
            {
                // Finalize current chunk
                byte[] chunkData = _currentChunkStream.ToArray();

                // OPTIMIZATION 6: Skip if mostly silent
                if (!IsAudioMostlySilent(chunkData))
                {
                    _audioChunks.Enqueue(chunkData);
                }

                // Reset for next chunk
                _currentChunkStream.SetLength(0);
                _currentChunkBytes = 0;
            }
        }
    }

    /// <summary>
    /// Converts audio from source format to Whisper format
    /// </summary>
    private byte[] ConvertAudioFormat(byte[] sourceBuffer, int bytesRecorded, WaveFormat sourceFormat)
    {
        // If formats match, just return a copy of the source buffer
        if (sourceFormat.Equals(_whisperFormat))
        {
            var result = new byte[bytesRecorded];
            Array.Copy(sourceBuffer, result, bytesRecorded);
            return result;
        }

        // Otherwise, perform format conversion
        using var bufferedProvider = new BufferedWaveProvider(sourceFormat);
        bufferedProvider.AddSamples(sourceBuffer, 0, bytesRecorded);

        using var converter = new MediaFoundationResampler(bufferedProvider, _whisperFormat)
        {
            ResamplerQuality = 60 // High quality
        };

        // Calculate output buffer size
        var sourceBytes = sourceFormat.AverageBytesPerSecond;
        var targetBytes = _whisperFormat.AverageBytesPerSecond;
        var ratio = (float)targetBytes / sourceBytes;
        var convertedSize = (int)(bytesRecorded * ratio * 1.2); // Add 20% buffer for safety

        var convertedBuffer = new byte[convertedSize];
        var bytesRead = converter.Read(convertedBuffer, 0, convertedSize);

        // Trim to actual size
        if (bytesRead < convertedSize)
        {
            var result = new byte[bytesRead];
            Array.Copy(convertedBuffer, result, bytesRead);
            return result;
        }

        return convertedBuffer;
    }

    /// <summary>
    /// Background task that processes audio chunks
    /// </summary>
    private async Task ProcessAudioChunksAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                // Check if we have chunks to process
                if (_audioChunks.TryDequeue(out var chunk))
                {
                    // OPTIMIZATION 4: Throttle processing
                    TimeSpan timeSinceLastProcess = DateTime.Now - _lastProcessingTime;
                    if (timeSinceLastProcess < TimeSpan.FromSeconds(5))
                    {
                        // Re-enqueue the chunk and wait
                        _audioChunks.Enqueue(chunk);
                        await Task.Delay(500, _cts.Token);
                        continue;
                    }

                    // OPTIMIZATION 6: Skip if mostly silent
                    if (IsAudioMostlySilent(chunk))
                    {
                        continue;
                    }

                    _lastProcessingTime = DateTime.Now;

                    // Create processor for this chunk with optimized settings
                    using var processor = _whisperFactory.CreateBuilder()
                        .WithLanguage("auto")
                        .BeamSize(1)  // Reduces search space for faster processing
                        .Strategy(Whisper.net.Ggml.SamplingStrategy.Greedy) // Faster decoding
                        .Build();

                    // Create a WAV file in memory with proper headers
                    // We need to use a temp stream and then copy because WaveFileWriter closes the stream when disposed
                    byte[] wavBytes;
                    using (var tempStream = new MemoryStream())
                    {
                        using (var writer = new WaveFileWriter(tempStream, _whisperFormat))
                        {
                            writer.Write(chunk, 0, chunk.Length);
                            writer.Flush();
                        }
                        wavBytes = tempStream.ToArray();
                    }

                    // Create a new memory stream with the WAV data
                    using var audioStream = new MemoryStream(wavBytes);

                    // Process the properly formatted WAV data
                    var segmentsResult = processor.ProcessAsync(audioStream);

                    var transcription = new System.Text.StringBuilder();
                    await foreach (var segment in segmentsResult.WithCancellation(_cts.Token))
                    {
                        transcription.Append(segment.Text + " ");
                    }

                    var result = transcription.ToString().Trim();

                    // If we got a result, raise the event
                    if (!string.IsNullOrWhiteSpace(result))
                    {
                        TranscriptionReceived?.Invoke(this, result);
                    }
                }
                else
                {
                    // No chunks to process, wait a bit
                    await Task.Delay(100, _cts.Token);
                }
            }
            catch (TaskCanceledException)
            {
                // Expected on shutdown
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing audio chunk: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Forces processing of the current audio buffer
    /// </summary>
    public void FlushBuffer()
    {
        lock (_currentChunkStream)
        {
            if (_currentChunkBytes > 0)
            {
                // Enqueue current chunk even if it's not full
                byte[] chunkData = _currentChunkStream.ToArray();
                _audioChunks.Enqueue(chunkData);

                // Reset for next chunk
                _currentChunkStream.SetLength(0);
                _currentChunkBytes = 0;
            }
        }
    }

    /// <summary>
    /// Disposes resources used by the transcription service
    /// </summary>
    public void Dispose()
    {
        // Cancel processing task
        _cts.Cancel();

        try
        {
            // Wait for processing task to complete
            _processingTask.Wait(1000);
        }
        catch (AggregateException)
        {
            // Expected when task is cancelled
        }

        // Dispose resources
        _currentChunkStream.Dispose();
        _cts.Dispose();
        _whisperFactory.Dispose();
    }
    /// <summary>
    /// Determines if audio contains voice activity
    /// </summary>
    private bool ContainsVoice(byte[] buffer)
    {
        if (buffer == null || buffer.Length < 2)
            return false;

        // Check a sample of the buffer for efficiency
        int sampleSize = Math.Min(buffer.Length, 4000); // Check at most 4000 bytes
        int step = buffer.Length / sampleSize;
        if (step < 1) step = 1;

        for (var i = 0; i < buffer.Length - 1; i += step * 2)
        {
            if (i + 1 >= buffer.Length)
                break;

            // Convert bytes to 16-bit sample
            short sample = (short)((buffer[i + 1] << 8) | buffer[i]);
            float amplitude = Math.Abs(sample / 32768.0f);

            if (amplitude > VOICE_THRESHOLD)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if audio is mostly silence
    /// </summary>
    private bool IsAudioMostlySilent(byte[] buffer)
    {
        if (buffer == null || buffer.Length < 2)
            return true;

        int totalSamples = buffer.Length / 2; // 16-bit samples
        int silentSamples = 0;

        for (var i = 0; i < buffer.Length - 1; i += 2)
        {
            if (i + 1 >= buffer.Length)
                break;

            // Convert bytes to 16-bit sample
            short sample = (short)((buffer[i + 1] << 8) | buffer[i]);
            float amplitude = Math.Abs(sample / 32768.0f);

            if (amplitude <= SILENCE_THRESHOLD)
                silentSamples++;
        }

        float silencePercentage = (float)silentSamples / totalSamples;
        return silencePercentage >= SILENCE_PERCENTAGE_THRESHOLD;
    }
}