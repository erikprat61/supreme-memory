using Whisper.net.Ggml;

namespace SoundTranscriber;

public class ModelManager
{
    /// <summary>
    /// Ensures the Whisper model exists, downloading it if needed
    /// </summary>
    /// <returns>The path to the Whisper model file</returns>
    public async Task<string> EnsureModelExists()
    {
        string modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ggml-base.bin");

        if (!File.Exists(modelPath))
        {
            Console.WriteLine("Downloading Whisper model (this may take a while)...");
            await DownloadModel(modelPath);
        }

        return modelPath;
    }

    /// <summary>
    /// Downloads the Whisper model
    /// </summary>
    /// <param name="path">The path where the model will be saved</param>
    private async Task DownloadModel(string path)
    {
        // Create HttpClient and downloader instance
        using var httpClient = new System.Net.Http.HttpClient();
        var downloader = new WhisperGgmlDownloader(httpClient);

        // Download the model
        using var modelStream = await downloader.GetGgmlModelAsync(GgmlType.Base);
        using var fileStream = File.Create(path);
        await modelStream.CopyToAsync(fileStream);
    }
}
