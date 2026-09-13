# Resume Excel Skill - 简历转Excel技能

将PDF简历批量提取信息并生成带照片的Excel汇总表。

## 调用方式

```
/resume-excel
```

## 功能概述

本技能用于处理招聘场景中的简历数据：
- 从PDF简历提取文本信息和照片
- 解析关键字段（姓名、年龄、学历、工作经历等）
- 生成结构化的Excel汇总表
- 提取候选人头像照片

## 输入要求

- 简历PDF文件存放于 `实战-简历skill/` 子目录
- 支持猎聘(Liepin)和前程无忧(51job)格式
- 输出目录结构：

```
项目目录/
├── 实战-简历skill/          # PDF简历原件
├── extracted_photos/       # 提取的全部图片
├── portraits/               # 候选人头像
├── resume_text_*.txt        # 提取的简历文本
└── 人才简历库_新.xlsx       # 生成的汇总表
```

## 执行步骤

### Step 1: 读取PDF简历
使用 `Read` 工具读取 `实战-简历skill/` 目录下所有PDF简历文件。

### Step 2: 提取照片
使用 PyMuPDF (fitz) 从PDF中提取图片：

```python
import fitz
doc = fitz.open(pdf_path)
for page_num, page in enumerate(doc):
    images = page.get_images(full=True)
    for img_index, img in enumerate(images):
        xref = img[0]
        base_image = doc.extract_image(xref)
        image_bytes = base_image["image"]
        image_ext = base_image["ext"]
        # 保存到 extracted_photos/
        with open(f"extracted_photos/page{page_num}_{img_index}.{image_ext}", "wb") as f:
            f.write(image_bytes)
```

**头像识别**：正方形比例(1:1)，尺寸通常在300-600px之间，保存到 `portraits/` 目录，文件名为 `姓名.jpg`。

### Step 3: 生成Excel
使用 openpyxl 创建Excel文件：

**列结构：**
| 列 | 字段 | 说明 |
|----|------|------|
| A | 照片 | 100x100正方形头像 |
| B | 姓名 | 候选人姓名 |
| C | 性别 | 男/女 |
| D | 年龄 | 如：24岁 |
| E | 最高学历 | 本科/硕士等 |
| F | 工作年限 | 如：2年经验 |
| G | 意向岗位 | 应聘职位 |
| H | 当前/最近职位 | 最近工作职位 |
| I | 工作经历 | 公司名称和职位 |
| J | 技能/简介 | 个人优势关键信息 |
| K | 简历来源 | 平台来源如：猎聘/51job |
| L | 联系电话 | 手机号 |
| M | 其他联系方式 | 邮箱等 |

**照片处理：**
1. 调整图片为100x100正方形
2. 设置行高为80
3. 图片尺寸调整为60x60嵌入A列

### Step 4: 数据字段解析

从简历文本中提取以下信息：

```
姓名：揭雅琴
性别：女
年龄：24岁
最高学历：本科（财务管理，江西科技师范大学）
工作年限：2年工作经验
应聘职位：应付会计（佛山）
当前职位：财务助理
工作经历：广东德力智慧股份有限公司（2024.04 - 2025.11）
联系电话：19970412531
邮箱：1600622628@qq.com
个人优势：往来账务、数据处理、Excel函数、票据审核等
```

## 依赖库

```bash
pip install PyMuPDF openpyxl Pillow
```

- **PyMuPDF (fitz)** - PDF读取和图片提取
- **openpyxl** - Excel创建
- **Pillow** - 图片处理（裁剪、缩放）

## 输出文件

**人才简历库_新.xlsx** - 包含所有候选人信息的汇总表

## 已知简历格式

| 姓名 | 平台 | 应聘岗位 | 特点 |
|------|------|----------|------|
| 揭雅琴 | 51job | 应付会计 | 24岁，2年经验 |
| 潘焕钊 | 51job | 应付会计 | 26岁，5年经验 |
| 陈洁 | 51job | 海外销售 | 25岁，2年经验 |
| 李志莹 | 猎聘 | 供应链专员 | 32岁，9年经验 |
| 李炼辉 | 猎聘 | 硬件工程师 | 32岁，8年经验 |
| 杨女士 | 猎聘 | 财务BP | 26岁，4年经验 |

## 注意事项

1. 简历文本可能包含重复的页眉/页脚（OCR提取痕迹），需过滤
2. 保密声明："仅供招聘专用，禁止外传"
3. 头像照片优先选择正方形比例的清晰照片
4. 手机号格式：11位数字