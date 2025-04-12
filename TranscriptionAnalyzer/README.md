# Transcription Analyzer

This application analyzes transcription text files using a local LLM to create comprehensive notes and summaries of the content.

## Features

- Loads all transcription files from the TranscriptionApp/output directory
- Combines multiple transcriptions for context-aware analysis
- Uses a local LLM (Llama 2) for analysis
- Generates structured notes including:
  - Main topics discussed
  - Key points and insights
  - Action items or decisions
  - Important dates or deadlines
- Saves analysis results to a text file
- GPU acceleration support (if available)

## Prerequisites

- Python 3.7 or higher
- A compatible LLM model file (GGUF format)
- CUDA-capable GPU (optional, for faster processing)

## Installation

1. Clone the repository
2. Install the required packages:
   ```bash
   pip install -r requirements.txt
   ```
3. Download a compatible LLM model:
   - Visit https://huggingface.co/TheBloke/Llama-2-7B-Chat-GGUF
   - Download a model file (recommended: llama-2-7b-chat.Q4_K_M.gguf)
   - Place it in a `models` directory in your project
4. Set the model path environment variable:
   ```bash
   # On Windows
   set LLM_MODEL_PATH=path/to/your/model.gguf

   # On Linux/Mac
   export LLM_MODEL_PATH=path/to/your/model.gguf
   ```

## Usage

1. Make sure you have transcription files in the TranscriptionApp/output directory
2. Run the analyzer:
   ```bash
   python analyze.py
   ```
3. The analysis will be saved to `analysis_output.txt` in the TranscriptionAnalyzer directory

## Output Format

The analysis output will be structured with the following sections:
- Main Topics
- Key Points and Insights
- Action Items
- Important Dates

## Notes

- The application uses Llama 2 by default, but you can use any compatible GGUF model
- GPU acceleration will be automatically used if available
- The analysis quality depends on the model used and the quality of the input transcriptions
- For better performance, consider using a larger model (13B or 70B parameters)
- The context window is set to 4096 tokens, adjust if needed for your model 