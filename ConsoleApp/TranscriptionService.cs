using System.Text;

namespace ConsoleApp;

public class TranscriptionService
{
    private readonly Whisper.net.WhisperFactory _whisperFactory;
    private readonly FileManager _fileManager;

    public TranscriptionService(Whisper.net.WhisperFactory whisperFactory, FileManager fileManager)
    {
        _whisperFactory = whisperFactory;
        _fileManager = fileManager;
    }

    /// <summary>
    /// Transcribes a single audio file
    /// </summary>
    public async Task TranscribeAudioFile(string audioFilePath)
    {
        string filename = Path.GetFileName(audioFilePath);
        Console.WriteLine($"Transcribing {filename}...");

        try
        {
            // Check if file exists before attempting to transcribe
            if (!File.Exists(audioFilePath))
            {
                Console.WriteLine($"Audio file not found: {filename}");
                return;
            }

            // Create a new processor using the factory for each transcription
            using var processor = _whisperFactory.CreateBuilder().WithLanguage("auto").Build();

            string transcriptionText;

            using (
                var fileStream = new FileStream(
                    audioFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read
                )
            )
            {
                var segmentsResult = processor.ProcessAsync(fileStream);

                // Build transcription from results
                StringBuilder transcription = new StringBuilder();
                await foreach (var segment in segmentsResult)
                {
                    transcription.Append(segment.Text + " ");
                }

                transcriptionText = transcription.ToString().Trim();
            }

            // Write transcription to file
            _fileManager.WriteTranscriptionToFile(transcriptionText, audioFilePath);

            Console.WriteLine($"Transcription completed for {filename}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error transcribing audio {filename}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    /// <summary>
    /// Processes and transcribes a multi-part recording
    /// </summary>
    public async Task ProcessMultiPartRecording(
        List<string> audioPaths,
        DateTime startTime,
        DateTime endTime,
        string outputDirectory
    )
    {
        if (audioPaths.Count == 0)
            return;

        Console.WriteLine($"Processing multi-part recording with {audioPaths.Count} parts...");

        try
        {
            // First, transcribe each part individually
            List<Task> transcriptionTasks = new List<Task>();
            foreach (string audioPath in audioPaths)
            {
                transcriptionTasks.Add(TranscribeAudioFile(audioPath));
            }

            // Wait for all individual transcriptions to complete
            await Task.WhenAll(transcriptionTasks);

            // Create combined transcription file
            _fileManager.CreateCombinedTranscriptionFile(
                audioPaths,
                startTime,
                endTime,
                outputDirectory
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in multi-part processing: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
