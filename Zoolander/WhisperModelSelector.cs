using Whisper.net.Ggml;

namespace Zoolander;

/// <summary>
/// Provides a centralized way to select and configure Whisper models
/// </summary>
public class WhisperModelSelector
{
    /// <summary>
    /// Available Whisper model types
    /// </summary>
    public enum ModelType
    {
        Tiny,
        Base,
        Small,
        Medium,
        Large,
        LargeV2,
        LargeV3,
        LargeV3Turbo
    }

    /// <summary>
    /// Gets the GgmlType from the selected model type
    /// </summary>
    /// <param name="modelType">The model type to use</param>
    /// <returns>The corresponding GgmlType</returns>
    public static GgmlType GetGgmlType(ModelType modelType)
    {
        return modelType switch
        {
            ModelType.Tiny => GgmlType.Tiny,
            ModelType.Base => GgmlType.Base,
            ModelType.Small => GgmlType.Small,
            ModelType.Medium => GgmlType.Medium,
            ModelType.LargeV3Turbo => GgmlType.LargeV3Turbo,
            ModelType.LargeV2 => GgmlType.LargeV2,
            ModelType.LargeV3 => GgmlType.LargeV3,
            _ => GgmlType.Base // Default to Base model
        };
    }

    /// <summary>
    /// Gets the filename for the selected model type
    /// </summary>
    /// <param name="modelType">The model type to use</param>
    /// <returns>Filename for the model</returns>
    public static string GetModelFilename(ModelType modelType)
    {
        return $"ggml-{modelType.ToString().ToLowerInvariant()}.bin";
    }

    /// <summary>
    /// Provides information about model size and requirements
    /// </summary>
    /// <param name="modelType">The model type</param>
    /// <returns>Information about the model</returns>
    public static (int sizeMB, int recommendedRAMMB, string description) GetModelInfo(ModelType modelType)
    {
        return modelType switch
        {
            ModelType.Tiny => (75, 512, "Fastest, lowest accuracy, smallest size"),
            ModelType.Base => (142, 1024, "Fast with decent accuracy"),
            ModelType.Small => (466, 2048, "Good balance of speed and accuracy"),
            ModelType.Medium => (1500, 4096, "High accuracy with moderate speed"),
            ModelType.Large => (3000, 8192, "Very high accuracy, slower processing"),
            ModelType.LargeV2 => (3100, 8192, "Improved version of Large model"),
            ModelType.LargeV3 => (3300, 8192, "Best accuracy, slowest processing"),
            _ => (142, 1024, "Default: Base model")
        };
    }

    /// <summary>
    /// Downloads the specified model if it doesn't exist
    /// </summary>
    /// <param name="modelType">Model type to download</param>
    /// <param name="targetDirectory">Directory to save model</param>
    /// <returns>Path to the model file</returns>
    public static async Task<string> EnsureModelExistsAsync(ModelType modelType, string? targetDirectory = null)
    {
        targetDirectory ??= AppDomain.CurrentDomain.BaseDirectory;
        var filename = GetModelFilename(modelType);
        var modelPath = Path.Combine(targetDirectory, filename);

        if (!File.Exists(modelPath))
        {
            var (sizeMB, _, _) = GetModelInfo(modelType);
            Console.WriteLine($"Downloading {modelType} Whisper model ({sizeMB} MB), please wait...");

            // Create HttpClient and downloader
            using var httpClient = new HttpClient();
            var downloader = new WhisperGgmlDownloader(httpClient);

            // Download the selected model
            using var modelStream = await downloader.GetGgmlModelAsync(GetGgmlType(modelType));
            using var fileStream = File.Create(modelPath);
            await modelStream.CopyToAsync(fileStream);

            Console.WriteLine($"{modelType} model downloaded successfully.");
        }

        return modelPath;
    }
}