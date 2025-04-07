namespace SoundTranscriber;

/// <summary>
/// This is a wrapper class to expose the Whisper.net.WhisperFactory for use in our application.
/// We need this because the original code uses the WhisperFactory directly, but our refactored
/// code needs to reference it from our namespace.
/// </summary>
public static class WhisperFactory
{
    /// <summary>
    /// Creates a WhisperFactory from a model file path
    /// </summary>
    /// <param name="modelPath">Path to the Whisper model file</param>
    /// <returns>A WhisperFactory instance</returns>
    public static Whisper.net.WhisperFactory FromPath(string modelPath)
    {
        return Whisper.net.WhisperFactory.FromPath(modelPath);
    }
}
