# Transcription Analyzer

A simple tool to analyze transcriptions using a local LLM.

## Requirements

- Python 3.8+
- A local LLM model file (e.g., Llama, Mistral, etc. in GGUF format)

## Installation

1. Clone this repository
2. Install the required dependencies:

```bash
pip install -r requirements.txt
```

## Usage

```bash
python TranscriptionAnalyzer/analyze.py path/to/transcription.txt --model path/to/model.gguf
```

### Options

- `transcription_file`: Path to the transcription file to analyze (required)
- `--model`: Path to the LLM model file (required)
- `--prompt`: Path to a custom prompt template file (optional)

## Example

```bash
python TranscriptionAnalyzer/analyze.py meetings/transcript.txt --model models/llama-2-7b-chat.gguf
```

## Getting a Model

You can download GGUF format models from:
- [TheBloke's Hugging Face page](https://huggingface.co/TheBloke)
- [Ollama](https://ollama.ai/)

Popular models include:
- Llama 2 (7B, 13B, 70B)
- Mistral (7B)
- Phi-2
- TinyLlama
