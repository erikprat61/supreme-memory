# Audio Transcription Tool

This Python application uses OpenAI's Whisper model to transcribe MP3 files recorded by the ConsoleApp.

## Setup

1. Install Python 3.8 or higher
2. Install the required dependencies:
   ```
   pip install --user -r requirements.txt
   ```
   
   Note: If you encounter permission errors, use the `--user` flag as shown above to install packages for your user only.

## Usage

1. Run the transcription script:
   ```
   python transcribe.py
   ```

The script will:
- Look for MP3 files in the `Data\AudioRecordings` directory
- Transcribe each MP3 file using Whisper
- Save the transcriptions as text files in the same directory as the MP3 files

## Notes

- The script uses Whisper's "base" model for transcription
- Transcriptions are saved as .txt files with the same name as the original MP3 files
- The script will process all MP3 files in all date subdirectories 