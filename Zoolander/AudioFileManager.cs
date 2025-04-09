namespace Zoolander;

public class AudioFileManager
{
    private readonly Lock _fileOperationLock = new();

    /// <summary>
    ///     Creates the output directory for audio recordings and transcriptions
    /// </summary>
    /// <returns>The path to the output directory</returns>
    public string CreateOutputDirectory()
    {
        // Create output directory with date
        var baseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "AudioTranscriptions",
            DateTime.Now.ToString("yyyy-MM-dd")
        );

        if (!Directory.Exists(baseDirectory))
            Directory.CreateDirectory(baseDirectory);

        return baseDirectory;
    }

    /// <summary>
    ///     Writes a transcription to a text file
    /// </summary>
    public void WriteTranscriptionToFile(string transcriptionText, string audioFilePath)
    {
        var transcriptionFilePath = Path.ChangeExtension(audioFilePath, ".txt");

        try
        {
            lock (_fileOperationLock)
            {
                // Use FileMode.Create to overwrite any existing file
                using var writer = new StreamWriter(
                    new FileStream(transcriptionFilePath, FileMode.Create, FileAccess.Write)
                );
                writer.Write(transcriptionText);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing transcription: {ex.Message}");
        }
    }

    /// <summary>
    ///     Creates a combined transcription file from one or more part transcriptions
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
            // Create a combined transcription file - always called "combined.txt"
            var baseName = $"{startTime:HH-mm-ss}_to_{endTime:HH-mm-ss}";
            var transcriptionFilePath = Path.Combine(
                outputDirectory,
                $"{baseName}_combined.txt"
            );

            // Create combined transcript
            lock (_fileOperationLock)
            {
                using var writer = new StreamWriter(transcriptionFilePath);
                // Always use the combined transcription header regardless of parts count
                writer.WriteLine(
                    $"--- Combined transcription of {audioPaths.Count} audio parts ---"
                );
                writer.WriteLine($"Recording time: {startTime} to {endTime}");
                writer.WriteLine();

                for (var i = 0; i < audioPaths.Count; i++)
                {
                    var partPath = audioPaths[i];
                    var partName = Path.GetFileName(partPath);

                    // Get transcription file path
                    var partTranscriptionPath = Path.ChangeExtension(partPath, ".txt");

                    // Always add part headers, even for single recordings
                    writer.WriteLine($"--- Part {i + 1}: {partName} ---");

                    if (File.Exists(partTranscriptionPath))
                    {
                        // Read existing transcription
                        var partTranscription = File.ReadAllText(partTranscriptionPath);
                        writer.WriteLine(partTranscription);
                    }
                    else
                    {
                        writer.WriteLine("[Transcription not available]");
                    }

                    // Always add a newline after each part
                    writer.WriteLine();
                }
            }

            Console.WriteLine($"Combined transcription completed: {transcriptionFilePath}");

            // Delete all part files (both WAV and TXT) after combined file is created
            DeletePartFiles(outputDirectory);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating combined transcription: {ex.Message}");
        }
    }

    /// <summary>
    ///     Deletes all files with "part" in the filename from the specified directory
    /// </summary>
    private static void DeletePartFiles(string directory)
    {
        try
        {
            // Get all files with "part" in the name (both WAV and TXT)
            string[] partFiles = Directory.GetFiles(directory, "*part*.*");

            Console.WriteLine($"Found {partFiles.Length} part files to delete");

            foreach (var file in partFiles)
                try
                {
                    File.Delete(file);
                    Console.WriteLine($"Deleted part file: {Path.GetFileName(file)}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"Error deleting file {Path.GetFileName(file)}: {ex.Message}"
                    );
                }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while trying to delete part files: {ex.Message}");
        }
    }
}
