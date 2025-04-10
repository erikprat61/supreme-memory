using System.Text;
using Whisper.net;

namespace Zoolander;

/// <summary>
/// Implementation of audio transcription service using Whisper.net
/// </summary>
public class WhisperTranscriptionService
{
    private readonly WhisperFactory _whisperFactory;

    /// <summary>
    /// Creates a new transcription service using the specified Whisper model
    /// </summary>
    /// <param name="modelPath">Path to the Whisper model file</param>
    public WhisperTranscriptionService(string modelPath)
    {
        // Initialize the Whisper factory with the model
        _whisperFactory = Whisper.net.WhisperFactory.FromPath(modelPath);
    }

    /// <summary>
    /// Transcribes an audio stream and returns the text as a string
    /// </summary>
    /// <param name="audioStream">Stream containing audio data to transcribe</param>
    /// <returns>A string containing the transcription text</returns>
    public async Task<string> TranscribeAudioAsync(Stream audioStream)
    {
        // Create a processor for this transcription
        using var processor = _whisperFactory.CreateBuilder()
            .WithLanguage("en") 
            .Build();

        // Process the audio stream
        var segmentsResult = processor.ProcessAsync(audioStream);

        // Build transcription from results
        var transcription = new StringBuilder();
        await foreach (var segment in segmentsResult)
        {
            transcription.Append(segment.Text + " ");
        }

        return transcription.ToString().Trim();
    }

    /// <summary>
    /// Transcribes an audio file and returns the text as a string
    /// </summary>
    /// <param name="audioFilePath">Path to the audio file to transcribe</param>
    /// <returns>A string containing the transcription text</returns>
    public async Task<string> TranscribeFileAsync(string audioFilePath)
    {
        if (!File.Exists(audioFilePath))
        {
            throw new FileNotFoundException("Audio file not found", audioFilePath);
        }

        using var fileStream = new FileStream(
            audioFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        );

        return await TranscribeAudioAsync(fileStream);
    }

/// <summary>
    /// Downloads the Whisper model if it doesn't exist
    /// </summary>
    /// <param name="modelPath">Path where the model should be stored</param>
    /// <returns>Path to the model file</returns>
    public static async Task<string> EnsureModelExistsAsync(string modelPath)
    {
        if (!File.Exists(modelPath))
        {
            Console.WriteLine("Downloading Whisper model (this may take a while)...");
            
            // Create HttpClient and downloader
            using var httpClient = new HttpClient();
            var downloader = new Whisper.net.Ggml.WhisperGgmlDownloader(httpClient);
            
            // Download the TINY model instead of base for better performance
            using var modelStream = await downloader.GetGgmlModelAsync(Whisper.net.Ggml.GgmlType.LargeV3);
            using var fileStream = File.Create(modelPath);
            await modelStream.CopyToAsync(fileStream);
            
            Console.WriteLine("Model downloaded successfully.");
        }
        
        return modelPath;
    }

    /// <summary>
    /// Disposes resources used by the transcription service
    /// </summary>
    public void Dispose()
    {
        _whisperFactory?.Dispose();
    }
}