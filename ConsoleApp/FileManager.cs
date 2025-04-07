namespace ConsoleApp;

public class FileManager
{
    private object _fileOperationLock = new object();

    /// <summary>
    /// Creates the output directory for audio recordings and transcriptions
    /// </summary>
    /// <returns>The path to the output directory</returns>
    public string CreateOutputDirectory()
    {
        // Create output directory with date
        string baseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "AudioTranscriptions",
            DateTime.Now.ToString("yyyy-MM-dd")
        );

        if (!Directory.Exists(baseDirectory))
            Directory.CreateDirectory(baseDirectory);

        return baseDirectory;
    }

    /// <summary>
    /// Creates a new wave file for recording
    /// </summary>
    public string CreateWaveFilePath(string outputDirectory, DateTime timestamp, int partNumber = 0)
    {
        string fileName;

        fileName = $"{timestamp:HH-mm-ss}_recording_part{partNumber}.wav";

        return Path.Combine(outputDirectory, fileName);
    }

    /// <summary>
    /// Renames a recording file with start and end timestamps
    /// </summary>
    public string RenameRecordingWithTimeRange(
        string originalPath,
        DateTime startTime,
        DateTime endTime,
        int partNumber = 0
    )
    {
        string directory = Path.GetDirectoryName(originalPath)!;
        string newFileName;

        if (partNumber > 0)
        {
            newFileName = $"{startTime:HH-mm-ss}_to_{endTime:HH-mm-ss}_part{partNumber}.wav";
        }
        else
        {
            newFileName = $"{startTime:HH-mm-ss}_to_{endTime:HH-mm-ss}.wav";
        }

        string newPath = Path.Combine(directory, newFileName);

        try
        {
            lock (_fileOperationLock)
            {
                File.Move(originalPath, newPath);
            }

            return newPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error renaming file: {ex.Message}");
            // If renaming fails, return the original path
            return originalPath;
        }
    }

    /// <summary>
    /// Writes a transcription to a text file
    /// </summary>
    public void WriteTranscriptionToFile(string transcriptionText, string audioFilePath)
    {
        string transcriptionFilePath = Path.ChangeExtension(audioFilePath, ".txt");

        try
        {
            lock (_fileOperationLock)
            {
                // Use FileMode.Create to overwrite any existing file
                using (
                    var writer = new StreamWriter(
                        new FileStream(transcriptionFilePath, FileMode.Create, FileAccess.Write)
                    )
                )
                {
                    writer.Write(transcriptionText);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing transcription: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a combined transcription file from multiple part transcriptions
    /// </summary>
    public void CreateCombinedTranscriptionFile(
        List<string> audioPaths,
        DateTime startTime,
        DateTime endTime,
        string outputDirectory
    )
    {
        if (audioPaths.Count == 0)
            return;

        try
        {
            // Create a combined transcription file
            string baseName = $"{startTime:HH-mm-ss}_to_{endTime:HH-mm-ss}_combined";
            string transcriptionFilePath = Path.Combine(outputDirectory, $"{baseName}.txt");

            // Create combined transcript
            lock (_fileOperationLock)
            {
                using (StreamWriter writer = new StreamWriter(transcriptionFilePath))
                {
                    writer.WriteLine(
                        $"--- Combined transcription of {audioPaths.Count} audio parts ---"
                    );
                    writer.WriteLine($"Recording time: {startTime} to {endTime}");
                    writer.WriteLine();

                    for (int i = 0; i < audioPaths.Count; i++)
                    {
                        string partPath = audioPaths[i];
                        string partName = Path.GetFileName(partPath);

                        // Get transcription file path
                        string partTranscriptionPath = Path.ChangeExtension(partPath, ".txt");

                        writer.WriteLine($"--- Part {i + 1}: {partName} ---");

                        if (File.Exists(partTranscriptionPath))
                        {
                            // Read existing transcription
                            string partTranscription = File.ReadAllText(partTranscriptionPath);
                            writer.WriteLine(partTranscription);
                        }
                        else
                        {
                            writer.WriteLine("[Transcription not available]");
                        }

                        writer.WriteLine();
                    }
                }
            }

            Console.WriteLine($"Combined transcription completed: {transcriptionFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating combined transcription: {ex.Message}");
        }
    }
}
