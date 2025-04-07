using SoundTranscriber;

Console.WriteLine("Audio Transcription Service Starting...");

// Create the application services
var fileManager = new FileManager();
var modelManager = new ModelManager();

// Ensure output directory exists
string baseDirectory = fileManager.CreateOutputDirectory();

// Check if model exists, download if needed
string modelPath = await modelManager.EnsureModelExists();

// Initialize Whisper model
Console.WriteLine("Loading Whisper model...");
using var whisperFactory = WhisperFactory.FromPath(modelPath);

Console.WriteLine("Whisper model loaded. Monitoring system audio...");
Console.WriteLine("Press ESC to exit.");

// Create transcription service
var transcriptionService = new TranscriptionService(whisperFactory, fileManager);

// Start monitoring audio
using var audioMonitor = new AudioMonitor(baseDirectory, transcriptionService);
await audioMonitor.StartMonitoring();

// Wait for ESC key to exit
while (Console.ReadKey(true).Key != ConsoleKey.Escape)
{
    Thread.Sleep(100);
}

Console.WriteLine("Shutting down...");
