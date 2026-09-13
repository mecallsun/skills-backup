# uni-app APK 构建经验方法论

> **来源**：JINGE 宿舍管理系统 v3.4.10 APK 构建实践
> **日期**：2026-08-15
> **适用**：所有 uni-app 项目 → Android APK 打包

---

## 一、核心问题与解法

### 问题 1：wgt ≠ APK

| 产物 | 是什么 | 大小 | 能否安装 |
|------|--------|------|---------|
| **wgt** | Web 资源包（HTML/JS/CSS/图片） | 1-2 MB | ❌ 需要 DCloud runtime |
| **APK** | 完整 Android 安装包 | 80-95 MB | ✅ 可直接安装 |

**根因**：wgt 只是 uni-app 编译出的 Web 资源，必须嵌入 DCloud 原生运行时（native bridge + Android 组件）才能在 Android 上运行。

**解法**：获取 DCloud 基座 APK → 解压 → 替换 wgt → 重打包 → 签名

### 问题 2：网络阻断导致无法下载基座

| 策略 | 路径/URL | 成功率 | 产物大小 |
|------|---------|--------|---------|
| **本地 HBuilderX** | `D:/Program Files/HBuilderX/plugins/launcher/base/android_base.apk` | 100% | 91.6 MB（含完整 runtime） |
| **DCloud 网络** | `https://nativesupport.dcloud.net.cn/.../5.0.173457.apk` | ~60%（国内阻断） | 约 100 MB |
| **从零构建** | 仅嵌入 wgt | 100% | 0.4-2 MB（**无 runtime，无法运行**） |

**最优策略**：本地 HBuilderX 基座优先 → 网络下载 fallback → 从零构建兜底（仅测试用）

### 问题 3：aapt2 中文路径失败

```
错误：系统找不到指定的文件
命令：aapt2 compile --dir "E:\AI工作目录\...\app" -o flat/ -A assets/
```

**根因**：Windows 上 `-A` 参数（添加额外资源路径）遇到中文目录名时路径解析失败。

**解法**：去掉 `-A` 参数，资源文件已在 `--dir` 目录下，无需额外指定。

### 问题 4：APK 大小异常

| APK 大小 | 含义 | 原因 |
|---------|------|------|
| 0.39 MB | 仅 wgt，无 runtime | fallback 从零构建成功但无 DCloud runtime |
| 94.38 MB | 完整 APK | 使用 HBuilderX 基座成功 |
| < 1 MB | 损坏/不完整 | 打包过程出错 |

**验证方法**：安装到手机，查看应用大小（设置→应用→智华宿舍→存储）

---

## 二、构建流水线（12 步）

```
1. 验证工具链（JDK 17 + Android SDK 34 + aapt2 + zipalign + apksigner + keytool）
2. 解析 manifest.json → appId + version
3. 清理 TEMP 目录
4. 获取基座 APK（本地 HBuilderX → 网络下载 → fallback）
5. 解压基座 APK（zipfile.extractall）
6. 替换 assets/apps/{appId}/www 为 wgt 内容
7. 生成 keystore（keytool，仅首次生成）
8. zip 重打包为 unsigned.apk
9. zipalign 4 字节对齐
10. apksigner 签名
11. 验证签名（apksigner verify -v）
12. 部署到 publish/latest/UniApp/ + 归档到 _archive/
```

---

## 三、关键代码片段

### 3.1 解析 wgt 信息

```python
import json
from pathlib import Path

def get_wgt_info(wgt_dir: Path) -> tuple[str, str]:
    """从 manifest.json 读取 appId 和 version"""
    with open(wgt_dir / 'manifest.json', 'r', encoding='utf-8') as f:
        data = json.load(f)
    app_id = data.get('app', {}).get('appid', '__UNI__APP')
    ver = data.get('version', {}).get('name', '1.0.0')
    return app_id, ver
```

### 3.2 基座获取策略

```python
LOCAL_BASE_APK = Path('D:/Program Files/HBuilderX/plugins/launcher/base/android_base.apk')

if LOCAL_BASE_APK.exists():
    # 策略 1：使用本地 HBuilderX 基座
    base_apk = LOCAL_BASE_APK
elif download_base_apk(DCLOUD_URL, dest):
    # 策略 2：网络下载 DCloud 基座
    base_apk = dest
else:
    # 策略 3：fallback 从零构建（APK 无法运行）
    signed_apk = build_from_scratch(wgt_id, wgt_dir)
    deploy_and_archive(signed_apk)
```

### 3.3 替换 wgt 资源

```python
apps_dir = base_dir / 'assets' / 'apps' / app_id
if apps_dir.exists():
    shutil.rmtree(apps_dir)           # 删除旧 wgt
www_dest = apps_dir / 'www'
www_dest.parent.mkdir(parents=True, exist_ok=True)
shutil.copytree(wgt_source, www_dest) # 嵌入新 wgt
```

### 3.4 签名配置（幂等生成）

```python
ks = TEMP_DIR / 'app-release.keystore'
if not ks.exists():
    run(f'"{KEYTOOL}" -genkey -v -keystore "{ks}" -alias app '
        f'-keyalg RSA -keysize 2048 -validity 10000 '
        f'-storepass {PASS} -keypass {PASS} '
        f'-dname "CN=Company"')
```

---

## 四、工具链路径配置

| 工具 | Windows 典型路径 | 环境变量 |
|------|-----------------|---------|
| JDK | `D:/Android/jdk-17` | `JAVA_HOME` |
| Android SDK | `D:/Android/Sdk` | `ANDROID_HOME` |
| Build-Tools | `D:/Android/Sdk/build-tools/34.0.0` | — |
| HBuilderX | `D:/Program Files/HBuilderX/` | — |

```python
# 工具路径（跨平台兼容）
ANDROID_SDK = Path(os.environ.get('ANDROID_HOME', 'D:/Android/Sdk'))
BUILD_TOOLS = ANDROID_SDK / 'build-tools' / '34.0.0'
JAVA_HOME = Path(os.environ.get('JAVA_HOME', 'D:/Android/jdk-17'))
AAPT2 = BUILD_TOOLS / 'aapt2.exe'
ZIPALIGN = BUILD_TOOLS / 'zipalign.exe'
APKSIGNER = BUILD_TOOLS / 'apksigner.bat'
KEYTOOL = JAVA_HOME / 'bin' / 'keytool.exe'
```

---

## 五、永久教训（经验沉淀）

### 5.1 构建策略

| 教训 | 说明 |
|------|------|
| **基座优先** | 永远优先使用 HBuilderX 本地基座，网络下载不稳定 |
| **大小验证** | APK < 5 MB = fallback 产物（无 runtime），必须修复 |
| **临时清理** | 每次运行前清理 TEMP 目录，避免残留文件干扰 |
| **keystore 幂等** | keytool 只在 keystore 不存在时生成，已存在跳过 |

### 5.2 常见错误处理

| 错误 | 根因 | 修复 |
|------|------|------|
| `Tunnel connection failed: 404` | DCloud 网络阻断 | 改用本地 HBuilderX 基座 |
| `APK 0.39 MB` | fallback 无基座 | 检查 LOCAL_BASE_APK 是否存在 |
| `aapt2: 系统找不到` | `-A` 参数中文路径失败 | 去掉 `-A`，资源已在 `--dir` |
| `PNG CRC error` | 硬编码 icon 字节 CRC 错误 | 从 wgt 的 `static/logo.png` 提取 |
| `apksigner: NOT SIGNED` | 签名流程中断 | 检查 keytool 生成 keystore 是否成功 |

### 5.3 生产环境检查清单

- [ ] APK 大小 > 80 MB（含 DCloud runtime）
- [ ] 安装到真机测试（模拟器可能跳过签名验证）
- [ ] keystore 使用正式签名（非临时生成）
- [ ] versionCode 递增（Android 要求）
- [ ] 归档历史 APK 到 `publish/_archive/`

---

## 六、适用场景

| 场景 | 是否适用 | 说明 |
|------|---------|------|
| uni-app → Android APK | ✅ 完全适用 | 核心能力 |
| uni-app → H5 | ✅ 已内置 | `npm run build:h5` |
| uni-app → 微信小程序 | ✅ 已内置 | `npm run build:mp-weixin` |
| iOS IPA | ❌ 不适用 | 需要 macOS + Xcode |
| 微信小程序 | ❌ 不适用 | 走官方构建流程 |
| Flutter / React Native | ❌ 不适用 | 各自的打包工具 |

---

## 七、相关文件索引

| 文件 | 路径 | 说明 |
|------|------|------|
| 构建脚本 | `scripts/build-apk-v3.3.7.py` | JINGE 项目专用版 |
| 通用 Launcher | `C:\Users\Mecall\.skills-pool\skills\04-VibeCoding\uniapp-apk-builder\uniapp_apk_launcher.py` | 通用可配置版 |
| Skill 定义 | `C:\Users\Mecall\.skills-pool\skills\04-VibeCoding\uniapp-apk-builder\SKILL.md` | Skill 元数据 |
| 经验文档 | 本文档 | 经验方法论 |
| 本机 HBuilderX | `D:/Program Files/HBuilderX/plugins/launcher/base/android_base.apk` | ~92 MB 基座 |

---

## 八、版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| v1.0 | 2026-08-15 | 初始版本，基于 v3.4.10 经验沉淀 |
