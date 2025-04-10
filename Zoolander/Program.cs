using NAudio.Wave;

namespace Zoolander;

public class Program
{
    private static bool _running = true;
    private static readonly ManualResetEvent _exitEvent = new ManualResetEvent(false);

    public static async Task Main(string[] args)
    {
        Console.WriteLine("Zoolander Speech Transcription Service");
        Console.WriteLine("This app listens to audio and transcribes it without saving audio files.");
        Console.WriteLine("Press Ctrl+C to exit the application");

        // Choose the model to use (can be changed to any model type)
        var modelType = WhisperModelSelector.ModelType.Small;
        var language = "en"; // Set a specific language code like "en" or "fr" for better results

        // Parse command line arguments
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].ToLower() == "--model" && i + 1 < args.Length)
            {
                if (Enum.TryParse<WhisperModelSelector.ModelType>(args[i + 1], true, out var parsedModel))
                {
                    modelType = parsedModel;
                    i++; // Skip the next argument since we've used it
                }
            }
            else if (args[i].ToLower() == "--language" && i + 1 < args.Length)
            {
                language = args[i + 1];
                i++; // Skip the next argument
            }
        }

        // Display model information
        var (sizeMB, ramMB, modelDescription) = WhisperModelSelector.GetModelInfo(modelType);
        Console.WriteLine($"Using {modelType} model: {modelDescription}");
        Console.WriteLine($"Model size: {sizeMB}MB, Recommended RAM: {ramMB}MB");
        Console.WriteLine($"Language setting: {language}");

        // Set up console cancellation handler
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true; // Prevent the process from terminating immediately
            _running = false;
            _exitEvent.Set();
            Console.WriteLine("Shutdown requested, finishing processing...");
        };

        // Initialize file manager for output directory
        var fileManager = new AudioFileManager();
        var outputDirectory = fileManager.CreateOutputDirectory();
        Console.WriteLine($"Transcriptions will be logged to: {outputDirectory}");

        // Initialize Whisper model
        Console.WriteLine("Initializing Whisper model...");
        var modelPath = await WhisperTranscriptionService.EnsureModelExistsAsync(modelType);

        // Create transcription service
        using var transcriptionService = new BufferedTranscriptionService(
            modelPath,
            language: language);

        // Set up transcription handling
        transcriptionService.TranscriptionReceived += (sender, text) =>
        {
            Console.WriteLine("\n------------------------------------------");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Transcription:");
            Console.WriteLine(text);
            Console.WriteLine("------------------------------------------\n");

            // Log transcriptions to file
            var logPath = Path.Combine(outputDirectory, $"transcript_log_{DateTime.Now:yyyy-MM-dd}.txt");
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {text}\n\n");
        };

        // Set up alternative control mechanism that doesn't rely on Console.KeyAvailable
        _ = Task.Run(() =>
        {
            try
            {
                // Try to use console input if available
                Console.WriteLine("Press Esc to exit, Spacebar to force process current buffer");

                // Run until cancellation is requested
                while (_running)
                {
                    try
                    {
                        // Try to read a key, but handle the case where console isn't available
                        var key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.Escape)
                        {
                            _running = false;
                            _exitEvent.Set();
                            break;
                        }
                        else if (key.Key == ConsoleKey.Spacebar)
                        {
                            Console.WriteLine("Manually processing current buffer...");
                            transcriptionService.FlushBuffer();
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // Console input not available, just sleep
                        Thread.Sleep(1000);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Input handler error: {ex.Message}");
                // Keep running even if input handling fails
            }
        });

        // Set up audio listener
        using var audioListener = new AudioListener();
        bool isListening = false;

        // Set up sound detection events
        audioListener.SoundDetected += (_, _) =>
        {
            if (!isListening)
            {
                isListening = true;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Sound detected - transcribing...");
            }
        };

        audioListener.SilenceDetected += (_, _) =>
        {
            if (isListening)
            {
                isListening = false;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Silence detected - processing...");
                transcriptionService.FlushBuffer();
            }
        };

        // Set up audio capture
        using var capture = new WasapiLoopbackCapture();
        capture.DataAvailable += (sender, e) =>
        {
            if (e.BytesRecorded > 0)
            {
                // Let the audio listener detect sound/silence
                audioListener.ProcessAudioData(sender, e);

                // Always feed data to the transcription service
                // It will buffer the audio and process it as needed
                transcriptionService.AddAudioData(e.Buffer, e.BytesRecorded, capture.WaveFormat);
            }
        };

        // Start capturing audio
        Console.WriteLine("Starting audio capture...");
        capture.StartRecording();
        Console.WriteLine("Listening for speech...");

        // Wait for exit signal
        _exitEvent.WaitOne();

        // Clean up
        Console.WriteLine("Stopping audio capture...");
        capture.StopRecording();

        Console.WriteLine("Processing final audio buffer...");
        transcriptionService.FlushBuffer();

        // Wait briefly for final processing
        await Task.Delay(1000);

        Console.WriteLine("Application shutting down.");
    }
}