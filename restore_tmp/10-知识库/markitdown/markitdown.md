---
name: markitdown
description: "Use when converting files to Markdown format - handling PDF, images, documents, or other file formats conversion."
---

# Markitdown Skill

Convert various file formats to Markdown for easy editing and processing.

## When to Use

- Converting PDF documents to Markdown
- Extracting text from images
- Processing Word documents
- Converting HTML to Markdown
- Handling mixed-format files

## Usage

```
/markitdown <file_path>
```

## Supported Conversions

### PDF Files
Extract text and structure from PDF documents.
```
/markitdown document.pdf
```

### Images
OCR and extract text from images (PNG, JPG, etc.)

### Documents
Convert Word, Excel, and other office formats

### HTML
Clean HTML to Markdown conversion

## Output

- Returns Markdown content
- Preserves structure and formatting
- Handles complex layouts
- Extracts images when applicable

## Notes

- For large PDFs, specify page ranges: `document.pdf pages 1-10`
- Maximum 20 pages per request
- Complex layouts may need manual review