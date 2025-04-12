import os
import glob
from pathlib import Path
from typing import List, Dict
import json
from llama_cpp import Llama
from sentence_transformers import SentenceTransformer
import torch

class TranscriptionAnalyzer:
    def __init__(self, model_path: str):
        """Initialize the TranscriptionAnalyzer with a local LLM model."""
        self.model = Llama(
            model_path="models/llama-2-7b-chat.gguf",
            n_ctx=4096,  # Context window
            n_threads=4   # Number of CPU threads to use
        )
        # Load sentence transformer for better text processing
        self.encoder = SentenceTransformer('all-MiniLM-L6-v2')

    def load_transcriptions(self, directory: str) -> List[Dict[str, str]]:
        """Load all transcription files from the specified directory."""
        transcriptions = []
        txt_files = glob.glob(os.path.join(directory, "*.txt"))
        
        for file_path in txt_files:
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()
                transcriptions.append({
                    'filename': os.path.basename(file_path),
                    'content': content
                })
        
        return transcriptions

    def analyze_transcriptions(self, transcriptions: List[Dict[str, str]]) -> str:
        """Analyze the transcriptions using the local LLM."""
        # Combine all transcriptions into a single context
        combined_text = "\n\n".join([f"File: {t['filename']}\n{t['content']}" for t in transcriptions])
        
        # Create a prompt for the LLM
        prompt = f"""Please analyze the following transcriptions and create comprehensive notes about:
1. Main topics discussed
2. Key points and insights
3. Action items or decisions made
4. Important dates or deadlines mentioned

Transcriptions:
{combined_text}

Please provide a well-structured summary of the above content."""

        # Generate response using local LLM
        response = self.model(
            prompt,
            max_tokens=2000,
            temperature=0.7,
            stop=["</s>", "Human:", "Assistant:"],
            echo=False
        )

        return response['choices'][0]['text'].strip()

    def save_analysis(self, analysis: str, output_file: str):
        """Save the analysis results to a file."""
        with open(output_file, 'w', encoding='utf-8') as f:
            f.write(analysis)

def main():
    # Check if CUDA is available
    if torch.cuda.is_available():
        print("CUDA is available. Using GPU acceleration.")
    else:
        print("CUDA is not available. Using CPU only.")

    # Path to the local LLM model
    model_path = os.getenv('LLM_MODEL_PATH', 'models/llama-2-7b-chat.gguf')
    
    if not os.path.exists(model_path):
        print(f"Error: Model file not found at {model_path}")
        print("Please download a compatible model and set the LLM_MODEL_PATH environment variable.")
        print("You can download models from: https://huggingface.co/TheBloke/Llama-2-7B-Chat-GGUF")
        return

    # Initialize analyzer
    analyzer = TranscriptionAnalyzer(model_path)

    # Load transcriptions from the TranscriptionApp/output directory
    transcriptions_dir = os.path.join('TranscriptionApp', 'output')
    transcriptions = analyzer.load_transcriptions(transcriptions_dir)

    if not transcriptions:
        print("No transcription files found!")
        return

    # Analyze transcriptions
    print("Analyzing transcriptions...")
    analysis = analyzer.analyze_transcriptions(transcriptions)

    # Save analysis
    output_file = os.path.join('TranscriptionAnalyzer', 'analysis_output.txt')
    analyzer.save_analysis(analysis, output_file)
    print(f"Analysis saved to {output_file}")

if __name__ == "__main__":
    main() 