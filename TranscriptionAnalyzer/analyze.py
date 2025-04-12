#!/usr/bin/env python3
"""
Simple script to analyze a transcription using a local LLM.
"""

import argparse
import os
import sys
from llama_cpp import Llama

def load_transcription(file_path):
    """Load transcription from a file."""
    print(f"Loading transcription from {file_path}...")
    if not os.path.exists(file_path):
        raise FileNotFoundError(f"Transcription file not found: {file_path}")
    
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
    print("Transcription loaded successfully.")
    return content

def analyze_transcription(transcription, model_path, prompt_template=None):
    """Analyze transcription using a local LLM."""
    try:
        # Default prompt template if none provided
        if prompt_template is None:
            prompt_template = """
            Below is a transcription. Please analyze it and provide:
            1. A summary of the main points
            2. Key topics discussed
            3. Any action items or decisions made
            
            Transcription:
            {transcription}
            
            Analysis:
            """
        
        print(f"Loading LLM model from {model_path}...")
        # Load the LLM
        llm = Llama(
            model_path=model_path,
            n_ctx=4096,  # Context window
            n_threads=4   # Number of CPU threads to use
        )
        print("Model loaded successfully.")
        
        # Format the prompt with the transcription
        print("Preparing prompt...")
        prompt = prompt_template.format(transcription=transcription)
        
        # Generate the analysis
        print("Generating analysis... (this may take a few moments)")
        response = llm(
            prompt,
            max_tokens=1024,
            temperature=0.7,
            stop=["</analysis>", "\n\n\n"]
        )
        
        return response['choices'][0]['text'].strip()
    except Exception as e:
        print(f"Error during analysis: {str(e)}", file=sys.stderr)
        raise

def main():
    parser = argparse.ArgumentParser(description='Analyze a transcription using a local LLM')
    parser.add_argument('transcription_file', help='Path to the transcription file')
    parser.add_argument('--model', required=True, help='Path to the LLM model file')
    parser.add_argument('--prompt', help='Path to a custom prompt template file')
    
    try:
        args = parser.parse_args()
        
        # Load the transcription
        transcription = load_transcription(args.transcription_file)
        
        # Load custom prompt if provided
        prompt_template = None
        if args.prompt:
            print(f"Loading custom prompt from {args.prompt}...")
            with open(args.prompt, 'r', encoding='utf-8') as f:
                prompt_template = f.read()
            print("Custom prompt loaded successfully.")
        
        # Analyze the transcription
        analysis = analyze_transcription(transcription, args.model, prompt_template)
        
        # Print the analysis
        print("\n=== Analysis ===\n")
        print(analysis)
        
    except Exception as e:
        print(f"Error: {str(e)}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
