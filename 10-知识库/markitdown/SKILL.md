---
name: markitdown
description: Convert files to Markdown.
---
# Markitdown

将文件转换为 Markdown 文本并输出内容。

## 用法

用户传入文件路径，调用 markitdown 提取文本内容并展示。

## 执行步骤

1. 获取用户提供的文件路径（从 `$ARGUMENTS` 中读取）
2. 运行以下命令提取内容：

```bash
C:/Users/noelh/AppData/Local/Programs/Python/Python314/python.exe -m markitdown "$ARGUMENTS"
```

3. 将输出内容展示给用户

## 注意

- 支持格式：.pptx、.docx、.pdf、.xlsx、.html 等
- 如果用户没有提供路径，提示用户输入文件路径
