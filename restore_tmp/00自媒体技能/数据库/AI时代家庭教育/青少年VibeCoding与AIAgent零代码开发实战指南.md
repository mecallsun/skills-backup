# 青少年 Vibe Coding 与 AI Agent 零代码开发实战指南

> **适合年龄：8-18岁**  
> **难度等级：入门到进阶**  
> **预计学习时间：4-6周**

---

## 目录

1. [引言：什么是 Vibe Coding 与 AI Agent](#引言)
2. [第一部分：Cursor 实战工作流 - 番茄钟App开发](#第一部分)
3. [第二部分：AI Agent 搭建实操 - 学习助手机器人](#第二部分)
4. [第三部分：AI 绘画与多模态实操 - 绘本创作](#第三部分)
5. [工具推荐与资源汇总](#工具推荐)
6. [最佳实践与注意事项](#最佳实践)
7. [总结与进阶路径](#总结)

---

## 引言：什么是 Vibe Coding 与 AI Agent {#引言}

### Vibe Coding：用自然语言编程

**Vibe Coding**（自然语言编程）是一种全新的编程方式，它让你可以用日常语言告诉AI你想要什么，AI就会帮你写出代码。就像和一位懂编程的朋友聊天一样！

**为什么适合青少年？**
- ✅ 不需要记忆复杂的语法规则
- ✅ 用中文或英文描述想法即可
- ✅ 可以快速看到成果，获得成就感
- ✅ 培养逻辑思维和问题解决能力

### AI Agent：你的智能助手

**AI Agent**（智能体）是一个可以自主完成任务的AI程序。你可以训练它：
- 📚 帮你整理学习笔记
- 🎯 生成练习题和测试题
- 📖 回答学科问题
- 🎨 创作故事和图片

**本指南将教会你：**
1. 使用 Cursor 开发一个完整的App
2. 在 Coze/Dify/扣子 上搭建专属学习助手
3. 用 AI 绘画工具创作自己的绘本

---

## 第一部分：Cursor 实战工作流 - 番茄钟App开发 {#第一部分}

### 1.1 什么是 Cursor？

**Cursor** 是一个AI驱动的代码编辑器，它就像你的编程伙伴，可以：
- 根据你的描述生成代码
- 帮你修复错误
- 解释代码的含义
- 优化代码性能

**安装步骤：**
1. 访问 [cursor.sh](https://cursor.sh)
2. 下载适合你操作系统的版本（Windows/Mac/Linux）
3. 安装并注册账号（可以使用GitHub账号登录）
4. 首次打开时，Cursor会引导你完成设置

### 1.2 项目准备：创建番茄钟App

#### 步骤1：创建项目文件夹

```
1. 打开 Cursor
2. 点击 File → New Folder
3. 命名为 "tomato-timer-app"
4. 打开这个文件夹
```

#### 步骤2：初始化项目

在 Cursor 的终端中输入：

```bash
npm init -y
```

这会创建一个 `package.json` 文件，记录你的项目信息。

### 1.3 需求文档（PRD）撰写

**什么是PRD？**  
PRD（Product Requirements Document）就是产品需求文档，用来描述你想要做什么。

**番茄钟App的PRD示例：**

```markdown
# 番茄钟专注力App需求文档

## 项目名称
番茄钟专注力计时器

## 目标用户
需要专注学习的青少年（8-18岁）

## 核心功能

### 1. 计时功能
- 默认25分钟倒计时（一个番茄时间）
- 可以自定义时间（5分钟、10分钟、25分钟、45分钟）
- 显示剩余时间（分钟:秒数格式）
- 时间到后播放提示音

### 2. 开始/暂停/重置
- 开始按钮：开始计时
- 暂停按钮：暂停计时
- 重置按钮：回到初始时间

### 3. 视觉反馈
- 圆形进度条显示剩余时间比例
- 时间快结束时颜色变红（最后1分钟）
- 完成时显示庆祝动画

### 4. 记录功能（可选）
- 记录今天完成的番茄数量
- 显示本周完成统计

## 技术要求
- 使用 HTML + CSS + JavaScript（纯前端，无需后端）
- 响应式设计（手机和电脑都能用）
- 界面简洁美观，适合青少年使用
```

**💡 小贴士：**  
写PRD时，要尽可能详细地描述你想要的功能。越详细，AI越能理解你的需求！

### 1.4 在 Cursor 中与 AI 协作开发

#### 第一次对话：创建基础HTML结构

**你的提示词：**

```
请帮我创建一个番茄钟计时器的HTML页面。要求：
1. 有一个大圆形显示剩余时间（25:00格式）
2. 有三个按钮：开始、暂停、重置
3. 页面背景使用柔和的渐变色
4. 使用现代化的设计风格
5. 所有代码写在一个HTML文件中
```

**AI可能会生成类似这样的代码：**

```html
<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>番茄钟专注力计时器</title>
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }

        body {
            font-family: 'Arial', sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            display: flex;
            justify-content: center;
            align-items: center;
            padding: 20px;
        }

        .container {
            text-align: center;
            background: white;
            padding: 40px;
            border-radius: 20px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
            max-width: 500px;
            width: 100%;
        }

        h1 {
            color: #333;
            margin-bottom: 30px;
            font-size: 28px;
        }

        .timer-display {
            width: 300px;
            height: 300px;
            border-radius: 50%;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            display: flex;
            justify-content: center;
            align-items: center;
            margin: 0 auto 30px;
            font-size: 48px;
            color: white;
            font-weight: bold;
            box-shadow: 0 10px 30px rgba(0,0,0,0.2);
        }

        .controls {
            display: flex;
            gap: 15px;
            justify-content: center;
        }

        button {
            padding: 12px 30px;
            font-size: 16px;
            border: none;
            border-radius: 25px;
            cursor: pointer;
            transition: all 0.3s;
            font-weight: bold;
        }

        .start-btn {
            background: #4CAF50;
            color: white;
        }

        .start-btn:hover {
            background: #45a049;
            transform: scale(1.05);
        }

        .pause-btn {
            background: #ff9800;
            color: white;
        }

        .pause-btn:hover {
            background: #e68900;
            transform: scale(1.05);
        }

        .reset-btn {
            background: #f44336;
            color: white;
        }

        .reset-btn:hover {
            background: #da190b;
            transform: scale(1.05);
        }
    </style>
</head>
<body>
    <div class="container">
        <h1>🍅 番茄钟专注力计时器</h1>
        <div class="timer-display" id="timer">25:00</div>
        <div class="controls">
            <button class="start-btn" id="startBtn">开始</button>
            <button class="pause-btn" id="pauseBtn">暂停</button>
            <button class="reset-btn" id="resetBtn">重置</button>
        </div>
    </div>

    <script>
        // JavaScript代码将在下一步添加
    </script>
</body>
</html>
```

**保存文件：**  
将代码保存为 `index.html`

**测试：**  
在浏览器中打开 `index.html`，你应该能看到一个漂亮的界面！

#### 第二次对话：添加计时功能

**你的提示词：**

```
现在需要添加倒计时功能：
1. 默认时间是25分钟（1500秒）
2. 点击"开始"按钮后开始倒计时
3. 点击"暂停"按钮暂停计时
4. 点击"重置"按钮恢复到25:00
5. 时间格式显示为 MM:SS（例如 25:00, 24:59）
6. 当时间到达00:00时，播放提示音并显示"时间到！"
```

**AI可能会生成这样的JavaScript代码：**

```javascript
let totalSeconds = 1500; // 25分钟 = 1500秒
let intervalId = null;
let isRunning = false;

const timerDisplay = document.getElementById('timer');
const startBtn = document.getElementById('startBtn');
const pauseBtn = document.getElementById('pauseBtn');
const resetBtn = document.getElementById('resetBtn');

// 格式化时间显示
function formatTime(seconds) {
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
}

// 更新显示
function updateDisplay() {
    timerDisplay.textContent = formatTime(totalSeconds);
    
    // 最后1分钟变红色
    if (totalSeconds <= 60) {
        timerDisplay.style.color = '#ff4444';
    } else {
        timerDisplay.style.color = 'white';
    }
}

// 开始计时
function startTimer() {
    if (isRunning) return;
    
    isRunning = true;
    intervalId = setInterval(() => {
        if (totalSeconds > 0) {
            totalSeconds--;
            updateDisplay();
        } else {
            clearInterval(intervalId);
            isRunning = false;
            timerDisplay.textContent = '时间到！🎉';
            timerDisplay.style.color = '#4CAF50';
            
            // 播放提示音
            const audio = new Audio('data:audio/wav;base64,UklGRnoGAABXQVZFZm10IBAAAAABAAEAQB8AAEAfAAABAAgAZGF0YQoGAACBhYqFbF1fdJivrJBhNjVgodDbq2EcBj+a2/LDciUFLIHO8tiJNwgZaLvt559NEAxQp+PwtmMcBjiR1/LMeSwFJHfH8N2QQAoUXrTp66hVFApGn+DyvmwhBSuBzvLZiTYIGGW57+OcTgwOUKjj8LZjHAY4kdfyzHksBSR3x/Dej0AKFF606euoVRQKRp/g8r5sIQUrgc7y2Yk2CBhlue/jnE4MDlCo4/C2YxwGOJHX8sx5LAUkd8fw3o9AChRetOnrqFUU');
            audio.play().catch(e => console.log('无法播放音频'));
        }
    }, 1000);
}

// 暂停计时
function pauseTimer() {
    if (intervalId) {
        clearInterval(intervalId);
        intervalId = null;
        isRunning = false;
    }
}

// 重置计时
function resetTimer() {
    pauseTimer();
    totalSeconds = 1500;
    updateDisplay();
    timerDisplay.style.color = 'white';
}

// 绑定事件
startBtn.addEventListener('click', startTimer);
pauseBtn.addEventListener('click', pauseTimer);
resetBtn.addEventListener('click', resetTimer);

// 初始化显示
updateDisplay();
```

**💡 小贴士：**  
如果代码有错误，直接告诉AI："这里报错了，错误信息是XXX，请帮我修复。"

#### 第三次对话：添加自定义时间功能

**你的提示词：**

```
请添加时间选择功能：
1. 在按钮上方添加4个时间选项按钮：5分钟、10分钟、25分钟、45分钟
2. 点击某个时间选项后，计时器重置为该时间
3. 选中的时间按钮要高亮显示
4. 如果正在计时，选择新时间应该先暂停并重置
```

**AI可能会添加这样的代码：**

```html
<!-- 在controls div之前添加 -->
<div class="time-options" style="margin-bottom: 20px;">
    <button class="time-btn" data-minutes="5">5分钟</button>
    <button class="time-btn" data-minutes="10">10分钟</button>
    <button class="time-btn active" data-minutes="25">25分钟</button>
    <button class="time-btn" data-minutes="45">45分钟</button>
</div>
```

```css
.time-options {
    display: flex;
    gap: 10px;
    justify-content: center;
    flex-wrap: wrap;
}

.time-btn {
    padding: 8px 20px;
    font-size: 14px;
    background: #e0e0e0;
    color: #333;
    border: 2px solid transparent;
}

.time-btn:hover {
    background: #d0d0d0;
}

.time-btn.active {
    background: #667eea;
    color: white;
    border-color: #764ba2;
}
```

```javascript
// 添加时间选择功能
const timeButtons = document.querySelectorAll('.time-btn');
let selectedMinutes = 25;

timeButtons.forEach(btn => {
    btn.addEventListener('click', () => {
        // 移除所有active类
        timeButtons.forEach(b => b.classList.remove('active'));
        // 添加active类到当前按钮
        btn.classList.add('active');
        // 更新选中的分钟数
        selectedMinutes = parseInt(btn.dataset.minutes);
        // 重置计时器
        resetTimer();
        totalSeconds = selectedMinutes * 60;
        updateDisplay();
    });
});
```

### 1.5 调试（Debug）实战对话

#### 场景1：计时器不工作

**问题：** 点击开始按钮后，计时器没有反应。

**你的提示词：**

```
我的计时器点击开始按钮后没有反应。请检查代码是否有问题。
当前代码：[粘贴你的代码]
浏览器控制台显示的错误：[如果有错误信息，粘贴在这里]
```

**AI的回复可能是：**

```
我发现问题了！在startTimer函数中，你需要先检查intervalId是否已经存在。
另外，确保updateDisplay函数在开始计时时也被调用一次。

请尝试这个修复：
```

```javascript
function startTimer() {
    if (isRunning) return;
    if (intervalId) return; // 防止重复启动
    
    isRunning = true;
    updateDisplay(); // 立即更新一次显示
    intervalId = setInterval(() => {
        // ... 其余代码保持不变
    }, 1000);
}
```

#### 场景2：时间显示格式错误

**问题：** 时间显示为 "25:0" 而不是 "25:00"

**你的提示词：**

```
时间显示格式有问题，显示的是"25:0"而不是"25:00"。
请检查formatTime函数。
```

**AI的修复：**

```javascript
function formatTime(seconds) {
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    // 使用padStart确保两位数显示
    return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
}
```

#### 场景3：暂停后无法继续

**问题：** 暂停后点击开始，计时器不继续倒计时。

**你的提示词：**

```
暂停后点击开始按钮，计时器没有继续倒计时，而是重新开始了。
请修复这个问题。
```

**AI的修复：**

```javascript
function pauseTimer() {
    if (intervalId) {
        clearInterval(intervalId);
        intervalId = null;
        isRunning = false; // 确保状态重置
    }
}

function startTimer() {
    if (isRunning) return;
    
    isRunning = true;
    // 如果已经有intervalId，先清除
    if (intervalId) {
        clearInterval(intervalId);
    }
    
    intervalId = setInterval(() => {
        // ... 倒计时逻辑
    }, 1000);
}
```

### 1.6 优化与美化

#### 添加进度条动画

**你的提示词：**

```
请添加一个圆形进度条，显示剩余时间的比例。
进度条应该围绕在时间显示的外圈，随着时间减少而减少。
```

**AI可能会添加SVG进度条：**

```html
<div class="timer-wrapper" style="position: relative; width: 300px; height: 300px; margin: 0 auto;">
    <svg class="progress-ring" width="300" height="300">
        <circle
            class="progress-ring-circle"
            stroke="#fff"
            stroke-width="8"
            fill="transparent"
            r="140"
            cx="150"
            cy="150"
            style="transform: rotate(-90deg); transform-origin: 50% 50%;"
        />
    </svg>
    <div class="timer-display" id="timer">25:00</div>
</div>
```

```css
.timer-wrapper {
    position: relative;
}

.progress-ring {
    position: absolute;
    top: 0;
    left: 0;
}

.progress-ring-circle {
    stroke-dasharray: 879.65; /* 2 * π * 140 */
    stroke-dashoffset: 0;
    transition: stroke-dashoffset 1s linear;
}
```

```javascript
// 更新进度条
function updateProgress() {
    const circle = document.querySelector('.progress-ring-circle');
    const circumference = 2 * Math.PI * 140;
    const progress = totalSeconds / (selectedMinutes * 60);
    const offset = circumference * (1 - progress);
    circle.style.strokeDashoffset = offset;
}

// 在updateDisplay函数中调用
function updateDisplay() {
    timerDisplay.textContent = formatTime(totalSeconds);
    updateProgress(); // 更新进度条
    // ... 其余代码
}
```

### 1.7 完成与测试清单

**功能测试清单：**
- [ ] 点击开始，计时器开始倒计时
- [ ] 点击暂停，计时器暂停
- [ ] 点击重置，计时器恢复到初始时间
- [ ] 选择不同时间选项，计时器正确更新
- [ ] 时间到达00:00时，显示"时间到！"
- [ ] 最后1分钟时，时间显示变红
- [ ] 进度条正确显示剩余时间比例

**浏览器兼容性测试：**
- [ ] Chrome浏览器
- [ ] Firefox浏览器
- [ ] Edge浏览器
- [ ] 手机浏览器（响应式设计）

**💡 小贴士：**  
如果某个功能不工作，不要着急！告诉AI具体的问题，它会帮你解决。

---

## 第二部分：AI Agent 搭建实操 - 学习助手机器人 {#第二部分}

### 2.1 什么是 AI Agent？

**AI Agent（智能体）** 是一个可以自主完成任务的AI程序。你可以：
- 🎯 给它设定目标和能力
- 📚 上传知识库让它学习
- 🔄 设计工作流程让它自动执行
- 💬 通过对话与它交互

**适合青少年的应用场景：**
- 📖 学习助手：回答问题、整理笔记
- 🎯 练习生成器：自动生成练习题
- 📝 错题本：记录和分析错题
- 🎨 创意助手：帮助写作和创作

### 2.2 平台选择：Coze vs Dify vs 扣子

#### Coze（字节跳动）

**优点：**
- ✅ 中文界面友好
- ✅ 功能强大，支持多模态
- ✅ 免费版功能充足
- ✅ 社区活跃，教程丰富

**适合：** 中文用户，需要多模态功能

**注册地址：** [coze.cn](https://www.coze.cn)

#### Dify

**优点：**
- ✅ 开源免费
- ✅ 可以本地部署
- ✅ 工作流功能强大
- ✅ 支持多种模型

**适合：** 有一定技术基础的用户

**注册地址：** [dify.ai](https://dify.ai)

#### 扣子（Coze国内版）

**优点：**
- ✅ 完全免费
- ✅ 界面简洁
- ✅ 适合初学者
- ✅ 中文支持好

**适合：** 完全零基础的青少年

**注册地址：** [coze.cn](https://www.coze.cn) （与Coze相同）

**本指南以扣子（Coze）为例，因为它的中文界面最适合青少年。**

### 2.3 案例：错题本学习助手机器人

#### 项目目标

创建一个AI助手，能够：
1. 📝 接收学生输入的错题
2. 🎯 分析错题的知识点
3. 📚 生成相似练习题（连线题、选择题）
4. 💡 提供解题思路和知识点讲解
5. 📊 统计错题频率，找出薄弱环节

#### 步骤1：注册和创建Bot

**操作步骤：**

```
1. 访问 coze.cn
2. 使用手机号或邮箱注册账号
3. 登录后，点击"创建Bot"
4. Bot名称：错题本学习助手
5. Bot头像：可以选择一个学习相关的图标
6. Bot描述：一个帮助你整理错题、生成练习题的学习助手
```

#### 步骤2：设定Bot的Persona（角色设定）

**Persona设定示例：**

```
你是一位耐心、友好的学习助手，专门帮助8-18岁的学生整理错题和学习。

你的特点：
- 用简单易懂的语言解释知识点
- 鼓励学生，而不是批评
- 用生动的例子帮助理解
- 会根据学生的年级调整难度

你的能力：
- 分析错题涉及的知识点
- 生成相似练习题（选择题、连线题、填空题）
- 提供详细的解题步骤
- 总结学习建议
```

**在扣子中的操作：**
1. 进入Bot设置
2. 找到"角色设定"或"Persona"
3. 粘贴上面的内容
4. 保存

#### 步骤3：设计对话流程（Prompt工程）

**主Prompt模板：**

```
# 错题本学习助手

## 用户输入格式
用户会以以下格式输入错题：
[题目]：[题目内容]
[我的答案]：[学生的错误答案]
[正确答案]：[正确答案]
[科目]：[数学/语文/英语等]

## 你的任务

### 1. 分析错题
- 识别题目涉及的知识点
- 分析错误原因（概念不清/计算错误/理解偏差等）
- 评估难度等级

### 2. 生成练习题
根据错题的知识点，生成3道相似题目：
- 1道选择题（4个选项）
- 1道连线题（5组对应关系）
- 1道填空题

### 3. 提供讲解
- 详细解释正确解法
- 指出学生答案的错误之处
- 提供知识点总结

### 4. 学习建议
- 推荐复习重点
- 建议练习方向
- 鼓励性话语

## 输出格式

请按照以下格式输出：

**📚 知识点分析**
[列出涉及的知识点]

**❌ 错误分析**
[分析错误原因]

**✅ 正确解法**
[详细步骤]

**📝 相似练习题**

**选择题：**
[题目]
A. [选项1]
B. [选项2]
C. [选项3]
D. [选项4]

**连线题：**
[左侧项目] ←→ [右侧项目]
...

**填空题：**
[题目]：____

**💡 学习建议**
[建议内容]

**🌟 加油！**
[鼓励话语]
```

**在扣子中的操作：**
1. 进入Bot的"提示词"设置
2. 将上面的模板粘贴进去
3. 可以根据需要调整
4. 保存

#### 步骤4：添加知识库（可选但推荐）

**知识库的作用：**  
让Bot学习特定学科的知识，回答更准确。

**创建知识库步骤：**

```
1. 在扣子中点击"知识库" → "新建知识库"
2. 知识库名称：初中数学知识点库
3. 上传方式选择：
   - 方式1：上传文档（PDF、Word、TXT）
   - 方式2：直接输入文本
   - 方式3：网页链接（如果有在线教材）
```

**知识库内容示例（数学）：**

```markdown
# 初中数学知识点库

## 第一章：有理数

### 1.1 正数和负数
- 正数：大于0的数，如 +3, +5.2
- 负数：小于0的数，如 -3, -5.2
- 0既不是正数也不是负数

### 1.2 有理数的加减法
- 同号两数相加，取相同的符号，并把绝对值相加
- 异号两数相加，绝对值相等时和为0；绝对值不等时，取绝对值较大的数的符号，并用较大的绝对值减去较小的绝对值

## 第二章：整式的加减

### 2.1 整式
- 单项式：只含有数字与字母的积的代数式
- 多项式：几个单项式的和

### 2.2 合并同类项
- 同类项：所含字母相同，并且相同字母的指数也相同的项
- 合并同类项：把同类项的系数相加，字母和字母的指数不变

[继续添加更多章节...]
```

**关联知识库到Bot：**
1. 在Bot设置中找到"知识库"
2. 选择你创建的知识库
3. 设置检索模式（建议选择"增强模式"）

#### 步骤5：设计工作流（Workflow）

**工作流的作用：**  
让Bot按照固定流程处理任务，更可靠。

**错题处理工作流设计：**

```
开始
  ↓
接收用户输入（错题信息）
  ↓
提取关键信息（题目、答案、科目）
  ↓
调用知识库检索相关知识点
  ↓
分析错题（使用AI分析）
  ↓
生成练习题（选择题、连线题、填空题）
  ↓
生成讲解和学习建议
  ↓
格式化输出
  ↓
结束
```

**在扣子中创建工作流：**

```
1. 进入Bot设置 → "工作流"
2. 点击"新建工作流"
3. 添加节点：

节点1：开始节点
  - 接收用户输入

节点2：信息提取节点
  - 使用LLM提取：题目、错误答案、正确答案、科目

节点3：知识库检索节点
  - 根据科目和题目内容检索相关知识

节点4：错题分析节点
  - 使用LLM分析错题原因和知识点

节点5：生成练习题节点
  - 使用LLM生成3道相似题目

节点6：生成讲解节点
  - 使用LLM生成详细讲解

节点7：格式化输出节点
  - 将结果格式化为易读的格式

节点8：结束节点
  - 返回结果给用户
```

**💡 小贴士：**  
如果工作流太复杂，可以先从简单的对话模式开始，熟练后再添加工作流。

#### 步骤6：测试Bot

**测试用例1：数学错题**

**输入：**
```
[题目]：计算 (-3) + 5
[我的答案]：-8
[正确答案]：2
[科目]：数学
```

**期望输出：**
- ✅ 识别知识点：有理数加法
- ✅ 分析错误：混淆了加法规则
- ✅ 生成3道相似题
- ✅ 提供详细讲解

**测试用例2：语文错题**

**输入：**
```
[题目]：下列词语中，哪个是反义词？
A. 高兴-快乐  B. 大-小  C. 美丽-漂亮  D. 跑-走
[我的答案]：A
[正确答案]：B
[科目]：语文
```

**期望输出：**
- ✅ 解释什么是反义词
- ✅ 分析为什么A不对
- ✅ 生成新的反义词练习题

#### 步骤7：优化和迭代

**常见优化方向：**

1. **提高准确性**
   - 添加更多知识库内容
   - 优化Prompt，让AI更准确理解任务

2. **改善用户体验**
   - 添加友好的开场白
   - 优化输出格式，更易读

3. **增加功能**
   - 添加错题统计功能
   - 生成错题报告
   - 推荐复习计划

**优化Prompt示例：**

```
# 优化后的Prompt

## 开场白
当用户第一次使用Bot时，说：
"你好！我是你的错题本学习助手 🎓
请按照以下格式输入你的错题：
[题目]：...
[我的答案]：...
[正确答案]：...
[科目]：...

我会帮你分析错题，并生成练习题哦！"

## 错误处理
如果用户输入格式不正确，友好地提示：
"看起来格式不太对呢 😊
请按照这个格式输入：
[题目]：你的题目
[我的答案]：你的答案
[正确答案]：正确答案
[科目]：科目名称"
```

### 2.4 高级功能：错题统计和分析

#### 功能设计

**目标：** 让Bot记住学生的错题，并生成统计报告。

**实现方式：**

**方法1：使用记忆功能（简单）**

```
在扣子中：
1. 开启Bot的"记忆"功能
2. 设置记忆内容：错题记录
3. Bot会自动记住对话中的错题
```

**方法2：使用数据库（进阶）**

```
需要技术基础，可以：
1. 使用扣子的"数据存储"功能
2. 或者连接外部数据库
3. 记录每次错题的信息
4. 定期生成统计报告
```

**统计报告示例Prompt：**

```
# 错题统计报告生成

当用户说"生成错题报告"时：

1. 回顾所有记录的错题
2. 统计：
   - 各科目错题数量
   - 高频错误知识点
   - 错误类型分布（概念不清/计算错误等）
3. 生成报告：

**📊 你的错题统计报告**

**总体情况**
- 总错题数：[数量]
- 涉及科目：[科目列表]

**薄弱知识点TOP 5**
1. [知识点1] - [错误次数]次
2. [知识点2] - [错误次数]次
...

**错误类型分析**
- 概念不清：[数量]
- 计算错误：[数量]
- 理解偏差：[数量]

**💡 学习建议**
根据统计结果，建议你重点复习：
1. [知识点1]
2. [知识点2]
...

**📚 推荐练习**
[根据薄弱环节推荐练习题]
```

### 2.5 分享和使用Bot

**发布Bot：**

```
1. 在扣子中点击"发布"
2. 选择发布方式：
   - 仅自己使用（私密）
   - 分享给朋友（需要链接）
   - 公开发布（所有人可用）
3. 获取Bot链接或二维码
4. 分享给同学或老师
```

**使用方式：**

```
方式1：在扣子网页中使用
方式2：添加到微信（如果支持）
方式3：通过API调用（进阶）
```

---

## 第三部分：AI 绘画与多模态实操 - 绘本创作 {#第三部分}

### 3.1 AI绘画工具选择

#### Midjourney

**优点：**
- ✅ 图片质量极高
- ✅ 艺术风格多样
- ✅ 社区活跃

**缺点：**
- ❌ 需要付费（$10/月起）
- ❌ 需要Discord账号
- ❌ 英文界面

**适合：** 有一定预算，追求高质量

#### 免费替代方案

**1. Stable Diffusion（本地/在线）**
- ✅ 完全免费
- ✅ 可本地运行
- ✅ 开源

**推荐平台：**
- [Hugging Face Spaces](https://huggingface.co/spaces)
- [Replicate](https://replicate.com)

**2. DALL-E 3（通过Bing/ChatGPT）**
- ✅ 免费（有限次数）
- ✅ 中文支持好
- ✅ 易于使用

**3. 国内平台**
- **文心一格**（百度）：免费，中文友好
- **通义万相**（阿里）：免费，中文友好
- **6pen**：免费，中文界面

**本指南以免费工具为主，重点介绍Stable Diffusion和国内平台。**

### 3.2 儿童友好的Prompt公式

#### Prompt基础结构

**公式模板：**

```
[主体描述] + [动作/场景] + [风格] + [细节] + [技术参数]
```

**示例：**

```
一只可爱的小兔子 + 在森林里采蘑菇 + 卡通风格 + 明亮的色彩，温暖的阳光 + 高清，4K
```

#### 青少年友好的Prompt模板库

**模板1：可爱动物角色**

```
[动物名称]，[形容词]（如：可爱、勇敢、聪明），[服装/特征]，[动作]，卡通风格，明亮色彩，适合儿童，高清
```

**示例：**
```
一只小熊猫，可爱，戴着红色围巾，在雪地里堆雪人，卡通风格，明亮色彩，适合儿童，高清
```

**模板2：奇幻场景**

```
[场景描述]，[角色]，[动作]，奇幻风格，[色彩描述]，充满想象力，适合儿童绘本，高清
```

**示例：**
```
一个魔法森林，小精灵，在花朵间飞舞，奇幻风格，粉紫色调，充满想象力，适合儿童绘本，高清
```

**模板3：学习场景**

```
[学习场景]，[角色]，[动作]，教育风格，[色彩描述]，温馨友好，适合儿童，高清
```

**示例：**
```
一间明亮的教室，小朋友，在认真写字，教育风格，暖色调，温馨友好，适合儿童，高清
```

#### 保持角色一致性的技巧

**技巧1：使用角色描述词**

```
第一页：一只叫"小星星"的小猫，白色，蓝色眼睛，戴着黄色蝴蝶结
第二页：小星星（同样的描述），在花园里
第三页：小星星（同样的描述），在看书
```

**技巧2：使用Seed值（Stable Diffusion）**

```
第一页生成后，记录Seed值
后续页面使用相同的Seed + 相同的角色描述
```

**技巧3：使用参考图（部分工具支持）**

```
第一页生成满意的角色后，保存图片
后续页面使用"以这张图为参考" + 新的场景描述
```

### 3.3 实战案例：创作10页图文绘本

#### 项目：小星星的冒险之旅

**故事大纲：**

```
第1页：介绍主角小星星（一只白色小猫）
第2页：小星星决定去冒险
第3页：小星星来到魔法森林
第4页：遇到新朋友小兔子
第5页：一起寻找宝藏
第6页：遇到困难（一条河）
第7页：想办法过河（搭桥）
第8页：找到宝藏（一箱书）
第9页：分享宝藏给朋友们
第10页：小星星回到家，讲述冒险故事
```

#### 第1页：角色介绍

**Prompt（文心一格）：**

```
一只白色的小猫，蓝色的大眼睛，戴着黄色蝴蝶结，坐在窗台上看星星，卡通风格，温馨的夜晚场景，明亮的星星背景，适合儿童绘本，高清，4K，柔和色彩
```

**Prompt（Stable Diffusion）：**

```
a white kitten with blue eyes wearing a yellow bow tie, sitting on a windowsill looking at stars, cartoon style, warm night scene, bright starry background, children's book illustration, high quality, 4K, soft colors, cute, friendly
```

**文字内容：**

```
这是小星星，一只充满好奇心的小猫。
她最喜欢在夜晚看星星，梦想着去冒险。
```

#### 第2页：决定冒险

**Prompt：**

```
同一只白色小猫（小星星），蓝色眼睛，黄色蝴蝶结，站在地图前，指着远方，卡通风格，明亮的房间，充满冒险精神，适合儿童绘本，高清
```

**文字内容：**

```
一天，小星星看着地图说：
"我要去冒险，看看外面的世界！"
```

**💡 保持一致性：**  
每次都要包含"白色小猫，蓝色眼睛，黄色蝴蝶结"这个描述。

#### 第3页：魔法森林

**Prompt：**

```
小星星（白色小猫，蓝色眼睛，黄色蝴蝶结）走进一个魔法森林，高大的彩色蘑菇，发光的萤火虫，奇幻风格，神秘而美丽，适合儿童绘本，高清
```

**文字内容：**

```
小星星走进了魔法森林。
这里的一切都那么神奇！
```

#### 第4-10页：继续创作

**按照同样的模式：**
1. 保持角色描述一致
2. 描述新的场景和动作
3. 添加适合绘本的风格词
4. 生成图片
5. 编写对应的文字

**完整Prompt列表：**

```
第4页：小星星遇到一只棕色的小兔子，在森林小径上，互相打招呼，卡通风格，友好温馨
第5页：小星星和小兔子一起看藏宝图，兴奋的表情，卡通风格，冒险氛围
第6页：小星星和小兔子遇到一条宽阔的河，看起来有点担心，卡通风格，挑战场景
第7页：小星星和小兔子用树枝搭桥，合作的样子，卡通风格，解决问题
第8页：小星星和小兔子找到一个大箱子，打开后是很多书，惊喜的表情，卡通风格，收获场景
第9页：小星星和小兔子把书分给森林里的其他动物，分享的快乐，卡通风格，温馨场景
第10页：小星星回到家，在窗台上给其他小猫讲故事，满足的表情，卡通风格，温馨结局
```

### 3.4 图片编辑和优化

#### 使用免费工具优化图片

**工具推荐：**

1. **Canva**（免费）
   - 添加文字
   - 调整颜色
   - 添加装饰

2. **Photopea**（免费，在线PS）
   - 专业编辑功能
   - 调整大小
   - 去除背景

3. **Remove.bg**（免费）
   - 一键去除背景
   - 适合制作透明背景

**操作步骤：**

```
1. 下载AI生成的图片
2. 打开Canva，选择"自定义尺寸"（如：1024x1024）
3. 上传图片
4. 添加文字框，输入绘本文字
5. 调整字体、颜色、位置
6. 可以添加装饰元素（星星、花朵等）
7. 导出为PNG或PDF
```

### 3.5 制作完整绘本

#### 方法1：使用Canva制作

```
1. 在Canva中创建新设计
2. 选择"演示文稿"模板，或自定义尺寸（如：21x21cm，适合打印）
3. 为每一页：
   - 上传对应的AI图片
   - 添加文字
   - 调整布局
4. 导出为PDF（适合打印）或图片序列
```

#### 方法2：使用PPT/Keynote

```
1. 创建新演示文稿
2. 设置幻灯片尺寸（如：21x21cm）
3. 每页插入：
   - AI生成的图片
   - 文字框（绘本文字）
4. 可以添加动画效果（可选）
5. 导出为PDF
```

#### 方法3：使用专业工具（进阶）

**工具：**
- **Book Creator**（在线，免费版有限制）
- **StoryJumper**（在线，免费）
- **Adobe InDesign**（专业，需付费）

### 3.6 分享和发布

#### 数字版本

```
1. 导出为PDF
2. 上传到：
   - Google Drive
   - 百度网盘
   - 或直接分享给朋友
```

#### 打印版本

```
1. 确保图片分辨率足够（至少300 DPI）
2. 选择合适的纸张（建议200g铜版纸）
3. 可以：
   - 在家用打印机打印
   - 送到打印店装订
   - 使用在线打印服务（如：淘宝打印店）
```

#### 在线发布

```
平台推荐：
1. 小红书：分享创作过程
2. B站：制作视频版绘本
3. 个人博客/网站
4. 社交媒体（微博、朋友圈）
```

---

## 工具推荐与资源汇总 {#工具推荐}

### Vibe Coding工具

| 工具 | 类型 | 难度 | 价格 | 推荐指数 |
|------|------|------|------|----------|
| **Cursor** | 代码编辑器 | ⭐⭐ | 免费（有限）/$20/月 | ⭐⭐⭐⭐⭐ |
| **GitHub Copilot** | 代码助手 | ⭐⭐ | $10/月 | ⭐⭐⭐⭐ |
| **Codeium** | 代码助手 | ⭐⭐ | 免费 | ⭐⭐⭐⭐ |
| **Replit** | 在线IDE | ⭐ | 免费（有限） | ⭐⭐⭐ |

### AI Agent平台

| 平台 | 语言 | 难度 | 价格 | 推荐指数 |
|------|------|------|------|----------|
| **扣子（Coze）** | 中文 | ⭐ | 免费 | ⭐⭐⭐⭐⭐ |
| **Dify** | 中英文 | ⭐⭐ | 免费（开源） | ⭐⭐⭐⭐ |
| **LangChain** | 英文 | ⭐⭐⭐ | 免费（需技术） | ⭐⭐⭐ |
| **AutoGPT** | 英文 | ⭐⭐⭐⭐ | 免费（需技术） | ⭐⭐ |

### AI绘画工具

| 工具 | 类型 | 难度 | 价格 | 推荐指数 |
|------|------|------|------|----------|
| **文心一格** | 在线 | ⭐ | 免费 | ⭐⭐⭐⭐⭐ |
| **通义万相** | 在线 | ⭐ | 免费 | ⭐⭐⭐⭐ |
| **Stable Diffusion** | 在线/本地 | ⭐⭐ | 免费 | ⭐⭐⭐⭐ |
| **Midjourney** | Discord | ⭐⭐ | $10/月起 | ⭐⭐⭐⭐⭐ |
| **DALL-E 3** | 在线 | ⭐ | 免费（有限） | ⭐⭐⭐⭐ |

### 学习资源

**中文资源：**
- [Cursor官方文档（中文社区）](https://cursor.sh)
- [扣子官方教程](https://www.coze.cn/docs)
- [AI绘画Prompt库](https://www.promptbase.com)（需翻译）

**英文资源：**
- [Cursor官方文档](https://docs.cursor.sh)
- [LangChain教程](https://python.langchain.com)
- [Stable Diffusion指南](https://stable-diffusion-art.com)

**视频教程：**
- B站搜索"Cursor教程"
- B站搜索"AI Agent搭建"
- B站搜索"AI绘画教程"

---

## 最佳实践与注意事项 {#最佳实践}

### 安全与隐私

**⚠️ 重要提醒：**

1. **不要分享个人信息**
   - 不要在提示词中输入真实姓名、地址、学校
   - 不要在代码中硬编码个人信息

2. **保护账号安全**
   - 使用强密码
   - 不要分享账号给他人
   - 定期检查账号活动

3. **内容审查**
   - 生成的内容要符合法律法规
   - 不要生成不当内容
   - 家长应该监督青少年的使用

### 学习建议

**1. 循序渐进**
- 从简单项目开始
- 逐步增加难度
- 不要急于求成

**2. 多实践**
- 理论重要，实践更重要
- 多做项目，积累经验
- 从错误中学习

**3. 记录学习过程**
- 写学习笔记
- 记录遇到的问题和解决方案
- 分享给同学，互相学习

**4. 寻求帮助**
- 遇到问题不要害怕
- 向老师、家长、同学求助
- 利用AI工具本身寻求帮助

### 常见问题FAQ

**Q1: Cursor生成的代码有错误怎么办？**
A: 直接告诉AI错误信息，它会帮你修复。也可以学习错误信息，提高自己的理解。

**Q2: AI Agent回答不准确怎么办？**
A: 优化Prompt，添加更多上下文。如果使用知识库，确保知识库内容准确。

**Q3: AI绘画的角色不一致怎么办？**
A: 使用详细的角色描述词，记录Seed值，或使用参考图功能。

**Q4: 需要编程基础吗？**
A: 不需要！这就是Vibe Coding的魅力。但了解基础概念会更有帮助。

**Q5: 这些工具都是免费的吗？**
A: 大部分有免费版本，但功能可能有限。对于学习来说，免费版通常足够。

---

## 总结与进阶路径 {#总结}

### 你已经学会了什么？

通过本指南，你应该已经掌握：

1. ✅ **Vibe Coding基础**
   - 使用Cursor开发完整应用
   - 与AI协作编写代码
   - 调试和优化代码

2. ✅ **AI Agent搭建**
   - 在扣子上创建Bot
   - 设计Prompt和工作流
   - 添加知识库和记忆功能

3. ✅ **AI绘画应用**
   - 编写有效的Prompt
   - 保持角色一致性
   - 创作完整绘本

### 下一步学习路径

**初级 → 中级：**
1. 开发更复杂的应用（如：待办清单、计算器）
2. 创建多功能的AI Agent（如：学习+娱乐助手）
3. 创作更长的故事和绘本

**中级 → 高级：**
1. 学习基础编程语言（Python、JavaScript）
2. 理解AI的工作原理
3. 自己训练AI模型（进阶）

**长期目标：**
- 成为AI时代的创造者
- 用AI解决实际问题
- 分享你的作品，帮助更多人

### 鼓励的话

**🎉 恭喜你完成了本指南的学习！**

记住：
- 💪 每个人都是从零开始的
- 🚀 持续练习会让你越来越强
- 🌟 你的创意和想法是最宝贵的
- 🤝 与AI协作，而不是依赖AI

**开始你的创作之旅吧！**

---

## 附录：完整代码示例

### 番茄钟App完整代码

[这里可以添加完整的HTML代码，但由于篇幅限制，已在第一部分详细展示]

### AI Agent Prompt模板库

[可以添加更多Prompt模板]

### AI绘画Prompt库

[可以添加更多绘画Prompt示例]

---

## 附录A：更多实战案例

### 案例1：待办清单App

**项目目标：** 创建一个可以添加、删除、标记完成任务的待办清单。

**核心功能：**
- 添加新任务
- 删除任务
- 标记任务为已完成
- 显示已完成/未完成任务数量
- 本地存储（刷新页面不丢失）

**开发步骤：**

**步骤1：创建HTML结构**

**提示词：**
```
创建一个待办清单App的HTML页面，包含：
1. 标题"我的待办清单"
2. 输入框和"添加"按钮
3. 任务列表区域
4. 统计信息（总任务数、已完成数）
5. 使用现代化的设计，适合青少年使用
```

**步骤2：添加JavaScript功能**

**提示词：**
```
添加待办清单的功能：
1. 点击"添加"按钮，将输入框的内容添加到列表
2. 每个任务项有：复选框（标记完成）、任务文本、删除按钮
3. 点击复选框，任务显示为已完成（划线和灰色）
4. 点击删除按钮，删除该任务
5. 实时更新统计信息
6. 使用localStorage保存数据，刷新页面后数据还在
```

**完整代码示例：**

```html
<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>待办清单</title>
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }

        body {
            font-family: 'Arial', sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            padding: 20px;
        }

        .container {
            max-width: 600px;
            margin: 0 auto;
            background: white;
            padding: 30px;
            border-radius: 20px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
        }

        h1 {
            text-align: center;
            color: #333;
            margin-bottom: 30px;
        }

        .input-section {
            display: flex;
            gap: 10px;
            margin-bottom: 20px;
        }

        #taskInput {
            flex: 1;
            padding: 12px;
            font-size: 16px;
            border: 2px solid #e0e0e0;
            border-radius: 8px;
        }

        #taskInput:focus {
            outline: none;
            border-color: #667eea;
        }

        .add-btn {
            padding: 12px 30px;
            background: #667eea;
            color: white;
            border: none;
            border-radius: 8px;
            cursor: pointer;
            font-size: 16px;
            font-weight: bold;
        }

        .add-btn:hover {
            background: #5568d3;
        }

        .stats {
            display: flex;
            justify-content: space-around;
            margin-bottom: 20px;
            padding: 15px;
            background: #f5f5f5;
            border-radius: 8px;
        }

        .stat-item {
            text-align: center;
        }

        .stat-number {
            font-size: 24px;
            font-weight: bold;
            color: #667eea;
        }

        .stat-label {
            font-size: 14px;
            color: #666;
        }

        .task-list {
            list-style: none;
        }

        .task-item {
            display: flex;
            align-items: center;
            padding: 15px;
            margin-bottom: 10px;
            background: #f9f9f9;
            border-radius: 8px;
            transition: all 0.3s;
        }

        .task-item:hover {
            background: #f0f0f0;
        }

        .task-item.completed {
            opacity: 0.6;
        }

        .task-item.completed .task-text {
            text-decoration: line-through;
            color: #999;
        }

        .task-checkbox {
            width: 20px;
            height: 20px;
            margin-right: 15px;
            cursor: pointer;
        }

        .task-text {
            flex: 1;
            font-size: 16px;
            color: #333;
        }

        .delete-btn {
            padding: 8px 15px;
            background: #f44336;
            color: white;
            border: none;
            border-radius: 5px;
            cursor: pointer;
            font-size: 14px;
        }

        .delete-btn:hover {
            background: #da190b;
        }
    </style>
</head>
<body>
    <div class="container">
        <h1>📝 我的待办清单</h1>
        
        <div class="input-section">
            <input type="text" id="taskInput" placeholder="输入新任务...">
            <button class="add-btn" id="addBtn">添加</button>
        </div>

        <div class="stats">
            <div class="stat-item">
                <div class="stat-number" id="totalTasks">0</div>
                <div class="stat-label">总任务</div>
            </div>
            <div class="stat-item">
                <div class="stat-number" id="completedTasks">0</div>
                <div class="stat-label">已完成</div>
            </div>
        </div>

        <ul class="task-list" id="taskList"></ul>
    </div>

    <script>
        let tasks = JSON.parse(localStorage.getItem('tasks')) || [];
        
        const taskInput = document.getElementById('taskInput');
        const addBtn = document.getElementById('addBtn');
        const taskList = document.getElementById('taskList');
        const totalTasksSpan = document.getElementById('totalTasks');
        const completedTasksSpan = document.getElementById('completedTasks');

        // 渲染任务列表
        function renderTasks() {
            taskList.innerHTML = '';
            tasks.forEach((task, index) => {
                const li = document.createElement('li');
                li.className = `task-item ${task.completed ? 'completed' : ''}`;
                
                li.innerHTML = `
                    <input type="checkbox" class="task-checkbox" ${task.completed ? 'checked' : ''} 
                           onchange="toggleTask(${index})">
                    <span class="task-text">${task.text}</span>
                    <button class="delete-btn" onclick="deleteTask(${index})">删除</button>
                `;
                
                taskList.appendChild(li);
            });
            
            updateStats();
            saveTasks();
        }

        // 更新统计
        function updateStats() {
            totalTasksSpan.textContent = tasks.length;
            completedTasksSpan.textContent = tasks.filter(t => t.completed).length;
        }

        // 添加任务
        function addTask() {
            const text = taskInput.value.trim();
            if (text === '') {
                alert('请输入任务内容！');
                return;
            }
            
            tasks.push({
                text: text,
                completed: false
            });
            
            taskInput.value = '';
            renderTasks();
        }

        // 切换任务完成状态
        function toggleTask(index) {
            tasks[index].completed = !tasks[index].completed;
            renderTasks();
        }

        // 删除任务
        function deleteTask(index) {
            tasks.splice(index, 1);
            renderTasks();
        }

        // 保存到本地存储
        function saveTasks() {
            localStorage.setItem('tasks', JSON.stringify(tasks));
        }

        // 事件监听
        addBtn.addEventListener('click', addTask);
        taskInput.addEventListener('keypress', (e) => {
            if (e.key === 'Enter') {
                addTask();
            }
        });

        // 初始化
        renderTasks();
    </script>
</body>
</html>
```

### 案例2：简单计算器

**项目目标：** 创建一个可以进行基本运算的计算器。

**开发提示词示例：**

```
创建一个计算器App，包含：
1. 显示屏显示当前输入和结果
2. 数字按钮（0-9）
3. 运算符按钮（+、-、×、÷）
4. 等号按钮（=）
5. 清除按钮（C）
6. 使用网格布局，按钮大小一致
7. 现代化的设计风格
```

### 案例3：随机抽奖器

**项目目标：** 输入多个选项，随机抽取一个。

**开发提示词示例：**

```
创建一个随机抽奖器：
1. 输入框可以输入多个选项（用逗号分隔）
2. "开始抽奖"按钮
3. 大屏幕显示抽奖结果
4. 抽奖时有动画效果（快速切换选项）
5. 最终停在随机选中的选项上
6. 可以记录历史抽奖结果
```

---

## 附录B：AI Agent进阶案例

### 案例1：英语学习助手

**功能设计：**
- 单词记忆助手
- 语法检查
- 作文批改
- 口语练习对话

**Prompt设计：**

```
# 英语学习助手

你是一位专业的英语老师，专门帮助8-18岁的学生提高英语水平。

## 你的能力

### 1. 单词记忆
- 解释单词的含义和用法
- 提供记忆技巧
- 生成例句
- 设计记忆游戏

### 2. 语法检查
- 检查句子语法错误
- 解释错误原因
- 提供正确表达

### 3. 作文批改
- 检查拼写和语法
- 评价内容结构
- 提供改进建议
- 给出评分（1-10分）

### 4. 口语对话
- 模拟日常对话场景
- 纠正发音建议
- 提供地道表达

## 交互方式

当用户说"学习单词：[单词]"时：
1. 解释单词含义（中英文）
2. 提供3个例句
3. 给出记忆技巧
4. 生成一道选择题测试

当用户说"检查语法：[句子]"时：
1. 检查语法错误
2. 如果有错误，指出并解释
3. 提供正确版本

当用户说"批改作文：[作文内容]"时：
1. 全面检查
2. 给出评分
3. 列出优点和不足
4. 提供改进建议

当用户说"口语练习：[场景]"时：
1. 模拟该场景对话
2. 引导用户参与
3. 纠正错误
4. 提供地道表达
```

### 案例2：科学实验助手

**功能设计：**
- 解释科学原理
- 设计实验步骤
- 分析实验结果
- 回答科学问题

**Prompt设计：**

```
# 科学实验助手

你是一位有趣的科学老师，帮助青少年理解科学原理和进行实验。

## 你的特点
- 用简单易懂的语言解释复杂概念
- 用生活中的例子帮助理解
- 鼓励动手实验
- 强调安全第一

## 功能

### 1. 解释原理
当用户问"为什么[现象]？"时：
- 用简单语言解释
- 举生活中的例子
- 可以画图说明（用文字描述）

### 2. 设计实验
当用户说"我想做[主题]的实验"时：
- 列出实验材料
- 详细步骤（安全提示）
- 预期结果
- 原理解释

### 3. 分析结果
当用户说"我的实验结果是[结果]"时：
- 分析结果是否正常
- 解释原因
- 如果异常，帮助找出问题
- 提供改进建议
```

---

## 附录C：AI绘画高级技巧

### 技巧1：多角色场景

**挑战：** 在同一画面中保持多个角色的一致性。

**解决方案：**

```
方法1：分步生成
1. 先生成每个角色的单独图片
2. 使用图片编辑工具（如Canva）组合
3. 添加背景和装饰

方法2：详细描述
在Prompt中详细描述每个角色：
"画面中有三个角色：
- 角色A：白色小猫，蓝色眼睛，黄色蝴蝶结
- 角色B：棕色小兔，粉色鼻子，绿色围巾
- 角色C：灰色小松鼠，大尾巴，红色帽子
三个角色在森林里野餐，温馨场景，卡通风格"
```

### 技巧2：连续场景的一致性

**保持背景一致：**

```
第1页：小星星在魔法森林入口，高大的彩色蘑菇，发光的萤火虫
第2页：小星星在魔法森林深处（同样的蘑菇和萤火虫），遇到小兔子
第3页：小星星和小兔子在魔法森林的小径上（延续前面的元素）

关键：每次都要提到"魔法森林，彩色蘑菇，发光萤火虫"
```

### 技巧3：风格统一

**建立风格关键词库：**

```
基础风格：卡通风格，适合儿童绘本，高清，4K
色彩风格：明亮色彩，柔和色调，温暖氛围
细节风格：简洁线条，可爱角色，温馨场景

每张图都包含这些关键词，确保风格统一。
```

### 技巧4：文字与图片的配合

**文字排版技巧：**

```
1. 文字位置：
   - 图片上方：适合标题
   - 图片下方：适合正文
   - 图片左侧/右侧：适合旁白

2. 字体选择：
   - 标题：粗体，大字号（24-36pt）
   - 正文：易读字体（16-20pt）
   - 对话：特殊字体，加引号

3. 颜色搭配：
   - 文字颜色要与图片对比明显
   - 深色图片用浅色文字
   - 浅色图片用深色文字
```

---

## 附录D：常见问题深度解答

### Q1: Cursor生成的代码运行不了怎么办？

**排查步骤：**

```
1. 检查浏览器控制台（F12）
   - 看是否有错误信息
   - 复制错误信息给AI

2. 检查文件路径
   - HTML文件是否正确保存
   - 引用的CSS/JS文件路径是否正确

3. 检查代码语法
   - 是否有拼写错误
   - 括号是否匹配
   - 引号是否配对

4. 询问AI
   提示词："我的代码报错了：[错误信息]，请帮我修复。代码：[粘贴代码]"
```

### Q2: AI Agent回答不准确，如何改进？

**改进方法：**

```
1. 优化Prompt
   - 添加更多上下文
   - 明确输出格式
   - 给出示例

2. 添加知识库
   - 上传相关文档
   - 确保知识库内容准确

3. 使用工作流
   - 分步骤处理
   - 每一步都验证结果

4. 迭代优化
   - 记录不准确的回答
   - 分析原因
   - 调整Prompt
```

### Q3: AI绘画的角色总是变，怎么办？

**解决方案：**

```
方案1：详细描述（最简单）
每次都用完全相同的角色描述词

方案2：使用Seed值（Stable Diffusion）
- 生成满意的角色后，记录Seed
- 后续使用相同Seed + 相同描述

方案3：使用参考图（部分工具支持）
- 保存满意的角色图片
- 后续使用"参考这张图" + 新场景

方案4：分步生成
- 单独生成每个角色
- 用图片编辑工具组合
```

### Q4: 如何让AI更好地理解我的需求？

**技巧：**

```
1. 结构化描述
   不要：做一个App
   要：创建一个待办清单App，包含输入框、添加按钮、任务列表...

2. 分步骤说明
   第一步：创建HTML结构
   第二步：添加样式
   第三步：添加功能

3. 提供示例
   "类似这样的效果：[描述或截图]"

4. 明确约束
   "不要使用jQuery"
   "必须支持手机端"
   "颜色要柔和"
```

### Q5: 免费工具有哪些限制？

**常见限制：**

```
Cursor免费版：
- 每月有限次数的AI请求
- 部分高级功能受限

AI Agent平台：
- 免费版通常有请求次数限制
- 知识库大小可能受限
- 部分高级功能需付费

AI绘画工具：
- 免费版通常有每日生成次数限制
- 图片分辨率可能受限
- 部分风格需付费

解决方案：
- 合理使用免费额度
- 多个工具轮换使用
- 学习基础技能，减少对AI的依赖
```

---

## 附录E：进阶学习路径

### 阶段1：基础掌握（1-2周）

**目标：**
- 熟悉Cursor基本操作
- 完成第一个简单项目
- 理解AI协作的基本流程

**项目：**
- 番茄钟App
- 待办清单
- 简单计算器

### 阶段2：技能提升（2-4周）

**目标：**
- 掌握Prompt编写技巧
- 能够调试和优化代码
- 创建AI Agent

**项目：**
- 更复杂的App（如：天气查询、笔记应用）
- 学习助手Agent
- 简单的AI绘画项目

### 阶段3：进阶应用（4-8周）

**目标：**
- 独立完成完整项目
- 优化用户体验
- 分享和发布作品

**项目：**
- 完整的Web应用
- 多功能的AI Agent
- 完整的绘本或故事书

### 阶段4：深入学习（8周+）

**目标：**
- 学习基础编程语言
- 理解AI工作原理
- 探索更高级的应用

**学习内容：**
- JavaScript/Python基础
- AI模型原理
- 高级Prompt工程
- 自定义AI应用

---

## 附录F：资源链接汇总

### 官方文档

- **Cursor**: https://docs.cursor.sh
- **扣子（Coze）**: https://www.coze.cn/docs
- **Dify**: https://docs.dify.ai
- **文心一格**: https://yige.baidu.com
- **Stable Diffusion**: https://stable-diffusion-art.com

### 学习社区

- **GitHub**: 搜索"cursor tutorial"、"ai agent tutorial"
- **B站**: 搜索相关教程视频
- **知乎**: 搜索"Cursor使用"、"AI Agent搭建"
- **小红书**: 搜索"AI绘画"、"Vibe Coding"

### Prompt资源

- **PromptBase**: https://www.promptbase.com（需翻译）
- **Awesome ChatGPT Prompts**: https://github.com/f/awesome-chatgpt-prompts
- **AI绘画Prompt库**: 各大AI绘画平台社区

### 工具集合

- **AI工具导航**: https://www.ai-toolbox.com
- **免费AI工具**: https://www.futurepedia.io
- **AI工具评测**: 各大科技媒体网站

---

## 附录G：项目模板库

### 模板1：简单网页App模板

```html
<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>[项目名称]</title>
    <style>
        /* 基础样式 */
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }

        body {
            font-family: 'Arial', sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            padding: 20px;
        }

        .container {
            max-width: 600px;
            margin: 0 auto;
            background: white;
            padding: 30px;
            border-radius: 20px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
        }

        /* 你的样式 */
    </style>
</head>
<body>
    <div class="container">
        <!-- 你的内容 -->
    </div>

    <script>
        // 你的JavaScript代码
    </script>
</body>
</html>
```

### 模板2：AI Agent基础Prompt模板

```
# [Bot名称]

你是一位[角色描述]，专门帮助[目标用户]完成[主要任务]。

## 你的特点
- [特点1]
- [特点2]
- [特点3]

## 你的能力
- [能力1]
- [能力2]
- [能力3]

## 交互方式

当用户[情况1]时：
1. [步骤1]
2. [步骤2]
3. [步骤3]

当用户[情况2]时：
1. [步骤1]
2. [步骤2]

## 输出格式

[格式要求]
```

### 模板3：AI绘画Prompt模板

```
[角色描述]，[动作/场景]，[风格]，[色彩]，[细节]，适合[用途]，高清，4K
```

**示例：**
```
一只白色小猫，蓝色眼睛，黄色蝴蝶结，在花园里玩耍，卡通风格，明亮色彩，温馨场景，适合儿童绘本，高清，4K
```

---

## 附录H：家长和老师指南

### 如何指导青少年学习？

**1. 鼓励探索**
- 不要限制孩子的想象力
- 鼓励尝试和犯错
- 从错误中学习

**2. 设定目标**
- 帮助设定合理的学习目标
- 分阶段完成
- 及时给予鼓励

**3. 监督安全**
- 确保使用安全的工具和平台
- 监督在线活动
- 保护隐私信息

**4. 共同学习**
- 与孩子一起探索
- 学习新技能
- 分享成果

### 适合的教学场景

**1. 编程课**
- 使用Cursor教授编程思维
- 不需要先学语法
- 快速看到成果

**2. AI应用课**
- 学习AI Agent的搭建
- 理解AI的工作原理
- 培养AI素养

**3. 创意课**
- AI绘画创作
- 故事编写
- 多媒体作品制作

### 评估标准

**初级：**
- 能够使用AI工具完成简单任务
- 理解基本概念
- 完成基础项目

**中级：**
- 能够独立完成项目
- 能够调试和优化
- 理解工作原理

**高级：**
- 能够创新应用
- 能够分享和教学
- 能够解决实际问题

---

**文档版本：** v1.0  
**最后更新：** 2026年2月  
**作者：** AI助手  
**许可：** 本指南可自由分享和学习使用

---

**💡 反馈和建议：**  
如果你在使用本指南时遇到问题，或有改进建议，欢迎反馈！

**🌟 分享你的作品：**  
完成项目后，记得分享给朋友和老师，让更多人看到你的成果！

**📚 持续学习：**  
AI技术发展很快，保持学习，跟上最新趋势！

**🎉 开始你的创作之旅吧！**
