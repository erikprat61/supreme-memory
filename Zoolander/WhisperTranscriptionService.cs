using System.Text;
using Whisper.net;

namespace Zoolander;

/// <summary>
/// Implementation of audio transcription service using Whisper.net
/// </summary>
public class WhisperTranscriptionService : IDisposable
{
    private readonly WhisperFactory _whisperFactory;
    private readonly string _language;

    /// <summary>
    /// Creates a new transcription service using the specified Whisper model
    /// </summary>
    /// <param name="modelPath">Path to the Whisper model file</param>
    /// <param name="language">Language for transcription (use "auto" for auto-detection)</param>
    public WhisperTranscriptionService(string modelPath, string language = "en")
    {
        // Initialize the Whisper factory with the model
        _whisperFactory = Whisper.net.WhisperFactory.FromPath(modelPath);
        _language = language;
    }

    /// <summary>
    /// Transcribes an audio stream and returns the text as a string
    /// </summary>
    /// <param name="audioStream">Stream containing audio data to transcribe</param>
    /// <param name="customLanguage">Optional language override</param>
    /// <returns>A string containing the transcription text</returns>
    public async Task<string> TranscribeAudioAsync(Stream audioStream, string? customLanguage = null)
    {
        // Create a processor for this transcription
        using var processor = _whisperFactory.CreateBuilder()
            .WithLanguage(customLanguage ?? _language)
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
    /// <param name="customLanguage">Optional language override</param>
    /// <returns>A string containing the transcription text</returns>
    public async Task<string> TranscribeFileAsync(string audioFilePath, string? customLanguage = null)
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

        return await TranscribeAudioAsync(fileStream, customLanguage);
    }

    /// <summary>
    /// Downloads the Whisper model if it doesn't exist
    /// </summary>
    /// <param name="modelType">Type of model to download</param>
    /// <param name="modelDirectory">Directory where the model should be stored</param>
    /// <returns>Path to the model file</returns>
    public static async Task<string> EnsureModelExistsAsync(
        WhisperModelSelector.ModelType modelType,
        string? modelDirectory = null)
    {
        return await WhisperModelSelector.EnsureModelExistsAsync(modelType, modelDirectory);
    }

    /// <summary>
    /// Disposes resources used by the transcription service
    /// </summary>
    public void Dispose()
    {
        _whisperFactory?.Dispose();
    }
}