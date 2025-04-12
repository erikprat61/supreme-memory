import os
import whisper
from datetime import datetime
from pathlib import Path
from tqdm import tqdm

def get_audio_files():
    """Get all MP3 files from the AudioRecordings directory in the Data folder."""
    # Use the absolute path to the Data directory
    audio_dir = os.path.join(r"C:\Users\a\repos\supreme-memory\Data", "AudioRecordings")
    
    print(f"Looking for audio files in: {audio_dir}")
    if not os.path.exists(audio_dir):
        print(f"Audio recordings directory not found at: {audio_dir}")
        return []
    
    # Get all MP3 files from all date subdirectories
    audio_files = []
    for date_dir in os.listdir(audio_dir):
        date_path = os.path.join(audio_dir, date_dir)
        if os.path.isdir(date_path):
            for file in os.listdir(date_path):
                if file.endswith('.mp3'):
                    full_path = os.path.join(date_path, file)
                    print(f"Found audio file: {full_path}")
                    print(f"File exists: {os.path.exists(full_path)}")
                    audio_files.append(full_path)
    
    return audio_files

def transcribe_audio(model, audio_file):
    """Transcribe a single audio file and save the transcription."""
    try:
        print(f"\nAttempting to transcribe: {audio_file}")
        print(f"File exists: {os.path.exists(audio_file)}")
        print(f"Absolute path: {os.path.abspath(audio_file)}")
        
        # Load and transcribe the audio
        result = model.transcribe(audio_file)
        
        # Create transcription file path in the Transcriptions directory
        audio_path = Path(audio_file)
        audio_filename = audio_path.name
        audio_date_dir = audio_path.parent.name
        
        # Create the Transcriptions directory structure
        transcriptions_dir = os.path.join(r"C:\Users\a\repos\supreme-memory\Data", "Transcriptions")
        date_dir = os.path.join(transcriptions_dir, audio_date_dir)
        
        # Create directories if they don't exist
        os.makedirs(date_dir, exist_ok=True)
        
        # Create the transcription file path with the same name but .txt extension
        transcription_path = os.path.join(date_dir, audio_path.stem + '.txt')
        
        # Save transcription
        with open(transcription_path, 'w', encoding='utf-8') as f:
            f.write(result["text"])
        
        print(f"Transcribed: {audio_path.name}")
        print(f"Saved to: {transcription_path}")
        return True
    except Exception as e:
        print(f"Error transcribing {audio_file}: {str(e)}")
        print(f"Error type: {type(e)}")
        import traceback
        print(f"Traceback: {traceback.format_exc()}")
        return False

def main():
    print("Loading Whisper model...")
    model = whisper.load_model("base")
    
    print("Finding audio files...")
    audio_files = get_audio_files()
    
    if not audio_files:
        print("No audio files found to transcribe.")
        return
    
    print(f"Found {len(audio_files)} audio files to transcribe.")
    
    # Transcribe each file
    successful = 0
    for audio_file in tqdm(audio_files, desc="Transcribing"):
        if transcribe_audio(model, audio_file):
            successful += 1
    
    print(f"\nTranscription complete!")
    print(f"Successfully transcribed {successful} out of {len(audio_files)} files.")

if __name__ == "__main__":
    main() 