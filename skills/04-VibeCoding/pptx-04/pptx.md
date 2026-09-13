---
name: pptx
description: "Use for any .pptx file operations - creating presentations, reading/parsing PPTX files, editing, modifying, templates, slides, layouts, speaker notes."
trigger: "whenever user mentions 'deck', 'slides', 'presentation', or references a .pptx filename"
---

# PPTX Skill

Create, read, edit, and manipulate PowerPoint presentations.

## When to Use

- Creating new presentations
- Reading/parsing existing .pptx files
- Editing slide content
- Modifying layouts or templates
- Adding speaker notes
- Combining or splitting decks
- Working with slide comments

## Usage

```
/pptx
```

## Operations

### Create Presentation
Build new slides with custom content, layouts, and formatting.

### Read Presentation
Extract text, structure, notes from existing .pptx files.

### Edit Slides
- Add/remove slides
- Modify text and formatting
- Change layouts
- Update images and graphics

### Templates
- Apply existing templates
- Create custom layouts
- Modify master slides

### Speaker Notes
- Add/edit speaker notes
- Export notes as Markdown

## File Format

PPTX files are ZIP archives containing XML files. I can:
- Read and parse the XML content
- Modify slide XML directly
- Rebuild and save modified presentations

## Notes

- Always invoke this skill when .pptx is involved
- Works with both input and output operations
- Handles both simple and complex presentations