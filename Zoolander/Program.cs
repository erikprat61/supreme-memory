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
        
        // Set up console cancellation handler
        Console.CancelKeyPress += (sender, e) => {
            e.Cancel = true; // Prevent the process from terminating immediately
            _running = false;
            _exitEvent.Set();
            Console.WriteLine("Shutdown requested, finishing processing...");
        };
        
        // Initialize file manager for output directory
        var fileManager = new AudioFileManager();
        var outputDirectory = fileManager.CreateOutputDirectory();
        Console.WriteLine($"Transcriptions will be logged to: {outputDirectory}");
        
        // Parse command line arguments for runtime duration
        int runTimeMinutes = 0;
        if (args.Length > 0 && int.TryParse(args[0], out runTimeMinutes) && runTimeMinutes > 0)
        {
            Console.WriteLine($"Application will automatically exit after {runTimeMinutes} minutes");
            
            // Set up a timer to exit the application
            Task.Run(async () => {
                await Task.Delay(TimeSpan.FromMinutes(runTimeMinutes));
                Console.WriteLine($"Automatic shutdown after {runTimeMinutes} minutes");
                _running = false;
                _exitEvent.Set();
            });
        }
        
        // Initialize Whisper model
        Console.WriteLine("Initializing Whisper model...");
        var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ggml-tiny.bin");
        await WhisperTranscriptionService.EnsureModelExistsAsync(modelPath);
        
        // Create transcription service
        using var transcriptionService = new BufferedTranscriptionService(modelPath);
        
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
        Task.Run(() => 
        {
            try 
            {
                // Try to use console input if available
                Console.WriteLine("Press Ctrl+C to exit the application");
                
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