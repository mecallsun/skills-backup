---
name: uniapp-apk-builder
description: uni-app 项目 APK 构建流水线。读取 manifest.json 获取 appId，自动选择 HBuilderX 本地基座或网络下载，解压替换 wgt 资源后重打包签名输出可安装 APK。支持 fallback 无基座从零构建。
license: MIT
ai_tier: tier-2
domain: vibe-coding
language: python
stage: build
output: apk
complexity: medium
platform: windows
priority: 75
tags:
  - uni-app
  - apk
  - android
  - dcloud
  - hbuilderx
  - mobile
  - build
  - python
triggers:
  - 编译 APK
  - 构建 APK
  - uniapp APK
  - uni-app APK
  - 打包 APK
  - 生成 APK
  - 发布 APK
references:
  - DCloud 官方文档: https://nativesupport.dcloud.net.cn/
  - HBuilderX 基座路径: D:/Program Files/HBuilderX/plugins/launcher/base/android_base.apk
---

# uniapp-apk-builder Skill

## 功能概述

将 uni-app 编译产物（wgt/web 资源）打包为可安装的 Android APK。

**核心能力：**
1. 读取 `manifest.json` 自动获取 `appId`（__UNI__XXX 格式）
2. 优先使用本地 HBuilderX 基座 APK（绕过网络阻断）
3. 解压基座 → 替换 wgt 资源 → 重打包 → zipalign → 签名
4. fallback：无基座时从零构建（aapt2 编译 + 手动打包）
5. 自动部署到 `publish/latest/UniApp/` 并归档

## 工作流程

```
1. 验证工具链（JDK/Android SDK）
2. 解析 manifest.json → wgt_id / version
3. 获取基座 APK（本地 HBuilderX > 网络下载 > fallback）
4. 解压基座，替换 assets/apps/{wgt_id}/www
5. 生成 keystore（如不存在）
6. zip 重打包为 unsigned.apk
7. zipalign 4 字节对齐
8. apksigner 签名
9. 验证签名有效性
10. 部署到 publish/latest/UniApp/
11. 归档到 publish/_archive/
```

## 关键参数（必须配置）

| 参数 | 说明 | 示例 |
|------|------|------|
| `WGT_SOURCE` | uni-app 编译产物目录（dist/build/app） | `ZEEHUA.UniApp-Vue/dist/build/app` |
| `VERSION` | APK 版本号（显示在手机上） | `3.4.10` |
| `VERSION_CODE` | APK versionCode（int，Android 要求递增） | `34100` |
| `PUBLISH_DIR` | APK 发布目标目录 | `publish/latest/UniApp` |
| `ANDROID_SDK` | Android SDK 根目录 | `D:/Android/Sdk` |
| `BUILD_TOOLS` | Build-Tools 版本 | `D:/Android/Sdk/build-tools/34.0.0` |
| `JAVA_HOME` | JDK 根目录 | `D:/Android/jdk-17` |
| `LOCAL_BASE_APK` | HBuilderX 本地基座路径 | `D:/Program Files/HBuilderX/plugins/launcher/base/android_base.apk` |

## 基座 APK 获取策略（优先级顺序）

| 优先级 | 来源 | 优点 | 缺点 |
|--------|------|------|------|
| **1️⃣ 本地 HBuilderX** | `D:/Program Files/HBuilderX/plugins/launcher/base/android_base.apk` | 绕过网络阻断，稳定 ~92MB，含完整 DCloud runtime | 需要本机安装 HBuilderX |
| **2️⃣ DCloud 网络下载** | `https://nativesupport.dcloud.net.cn/Android/studio/zip/5.0.173457.apk` | 官方最新基座 | 国内可能网络阻断 |
| **3️⃣ Fallback 从零构建** | 仅嵌入 wgt（约 1-2MB，无 runtime） | 无需任何外部依赖 | APK 无法运行（无 DCloud runtime），仅用于测试 |

## 签名配置

默认使用自动生成的 keystore（首次运行生成，存储在 TEMP 目录）：

| 字段 | 值 |
|------|-----|
| keystore | `TEMP/zeehua-release.keystore` |
| alias | `zeehua` |
| storepass/keypass | `zeehua@2026` |
| keyalg | RSA 2048bit |
| validity | 10000 天 |

**生产环境建议**：使用正式的签名 keystore（.jks/.keystore），不要使用临时生成的。

## 输出产物

```
publish/latest/UniApp/
└── ZEEHUA-{VERSION}.apk     ← 可安装 APK

publish/_archive/
└── ZEEHUA-APP-{VERSION}-final_{timestamp}.apk   ← 归档副本
```

## 错误处理

| 错误 | 原因 | 解决方案 |
|------|------|---------|
| `aapt2: 系统找不到指定的文件` | 中文路径导致 `-A` 参数失败 | 去掉 `-A` 参数，资源已在 unaligned_dir |
| `Tunnel connection failed: 404` | DCloud 网络阻断 | 自动 fallback 到本地 HBuilderX 或从零构建 |
| `APK 0.39 MB`（无基座构建） | wgt 仅 1.37MB 无 DCloud runtime | 必须使用 HBuilderX 基座或网络下载基座 |
| `PNG CRC error` | 硬编码 icon 字节 CRC 错误 | 从 wgt 的 `static/logo.png` 提取有效 PNG |
| `apksigner: NOT SIGNED` | 签名流程中断 | 检查 keytool 生成 keystore 是否成功 |

## 永久教训（经验沉淀）

1. **APK 大小判断**：< 5 MB 一定是 fallback 无基座产物；> 80 MB 才是含 DCloud runtime 的完整 APK
2. **aapt2 中文路径**：`compile --dir` 的 `-A` 参数对中文路径无效，须去掉 `-A`，资源文件已在目录内
3. **HBuilderX 优先**：本机有 HBuilderX 时直接用本地基座，网络阻断不影响
4. **keystore 幂等**：keytool 只在 keystore 不存在时生成，已存在的跳过
5. **wgt_id 来源**：从 `manifest.json` 的 `id` 字段读取，不是目录名
6. **manifest.json 编码**：`manifest.json` 是 UTF-8，直接 `json.load` 无需转码
7. **temp 清理**：每次运行清理 TEMP 目录，避免残留导致构建异常
8. **APK 安装测试**：签名后必须安装到真机测试，模拟器可能跳过签名验证

## 使用方式

```bash
# 方式 1：直接运行脚本
python scripts/build-apk-v3.3.7.py

# 方式 2：修改关键参数后运行
# 编辑脚本顶部的常量：
#   VERSION = '3.5.0'
#   VERSION_CODE = '35000'
#   WGT_SOURCE = 'your-uniapp/dist/build/app'
```

## 依赖工具链

| 工具 | 最低版本 | 路径 |
|------|---------|------|
| Python | 3.8+ | - |
| JDK | 17+ | `D:/Android/jdk-17` |
| Android SDK | 34 | `D:/Android/Sdk` |
| aapt2 | 34.0.0 | `build-tools/34.0.0/aapt2.exe` |
| zipalign | 34.0.0 | `build-tools/34.0.0/zipalign.exe` |
| apksigner | 34.0.0 | `build-tools/34.0.0/apksigner.bat` |
| keytool | JDK 内置 | `jdk-17/bin/keytool.exe` |
| HBuilderX（可选） | 5.0+ | `D:/Program Files/HBuilderX/` |

## 适用场景

✅ **完全适用**：uni-app 编译产物打包为可安装 APK（Android）
✅ **适用**：需要嵌入 DCloud runtime 的任何跨平台 App
❌ **不适用**：iOS IPA 打包（需要 macOS + Xcode）
❌ **不适用**：微信小程序（已有官方构建流程）

## 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| v1.0 | 2026-08-15 | 初始版本，基于 v3.4.10 经验沉淀 |
