#!/usr/bin/env python3
"""
uniapp-apk-builder Launcher
通用 uni-app APK 构建脚本，支持命令行参数配置

用法：
    python uniapp_apk_launcher.py --wgt-source <path> --version <ver> [--app-id <id>]

示例：
    python uniapp_apk_launcher.py --wgt-source ZEEHUA.UniApp-Vue/dist/build/app --version 3.4.10
"""
import argparse
import json
import os
import shutil
import subprocess
import urllib.request
import zipfile
from datetime import datetime
from pathlib import Path


def run(cmd: str, cwd: Path | None = None, check: bool = True) -> subprocess.CompletedProcess:
    """执行 shell 命令"""
    print(f'  > {cmd[:100]}{"..." if len(cmd) > 100 else ""}')
    result = subprocess.run(cmd, shell=True, cwd=cwd, capture_output=True,
                            text=True, encoding='utf-8', errors='replace')
    if check and result.returncode != 0:
        print(f'  ERROR: {result.stderr}')
        raise RuntimeError(f'cmd failed: {cmd[:80]}')
    return result


def resolve_tool_paths() -> dict:
    """自动解析 Android SDK / JDK 路径"""
    sdk = Path(os.environ.get('ANDROID_HOME', 'D:/Android/Sdk'))
    jdk = Path(os.environ.get('JAVA_HOME', 'D:/Android/jdk-17'))

    # 找最新的 build-tools
    build_tools_dir = sdk / 'build-tools'
    if build_tools_dir.exists():
        versions = sorted(build_tools_dir.iterdir(), reverse=True)
        bt = versions[0] if versions else sdk / 'build-tools' / '34.0.0'
    else:
        bt = sdk / 'build-tools' / '34.0.0'

    return {
        'aapt2':    bt / 'aapt2.exe',
        'apksigner': bt / 'apksigner.bat',
        'zipalign': bt / 'zipalign.exe',
        'keytool':  jdk / 'bin' / 'keytool.exe',
        'android_jar': sdk / 'platforms' / 'android-34' / 'android.jar',
    }


def get_wgt_info(wgt_dir: Path) -> tuple[str, str]:
    """从 manifest.json 读取 appId 和 version"""
    mf = wgt_dir / 'manifest.json'
    if not mf.exists():
        raise FileNotFoundError(f'manifest.json not found: {mf}')
    with open(mf, 'r', encoding='utf-8') as f:
        data = json.load(f)
    app_id = data.get('app', {}).get('appid', '__UNI__APP')
    ver = data.get('version', {}).get('name', '1.0.0')
    return app_id, ver


def download_base_apk(url: str, dest: Path) -> bool:
    """下载 DCloud 基座 APK"""
    print(f'\n[下载] DCloud base APK: {url}')
    try:
        urllib.request.urlretrieve(url, str(dest))
        print(f'  OK: {dest.stat().st_size / 1024 / 1024:.1f} MB')
        return True
    except Exception as e:
        print(f'  FAILED: {e}')
        return False


def extract_apk(apk: Path, out: Path):
    """解压 APK"""
    if out.exists():
        shutil.rmtree(out, ignore_errors=True)
    out.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(apk, 'r') as z:
        z.extractall(out)
    apps = out / 'assets' / 'apps'
    existing = [p.name for p in apps.iterdir()] if apps.exists() else []
    print(f'  Extracted, existing apps: {existing}')


def build_from_scratch(
    wgt_dir: Path,
    app_id: str,
    version: str,
    version_code: str,
    tools: dict,
    temp_dir: Path
) -> Path:
    """无基座 fallback：从零构建（仅嵌 wgt，无 DCloud runtime）"""
    print('\n[FALLBACK] 构建中（无 DCloud runtime，APK 约 1-2MB）...')
    unaligned = temp_dir / 'unaligned'
    aligned = temp_dir / 'aligned'
    signed = temp_dir / 'signed'
    for d in [unaligned, aligned, signed]:
        if d.exists():
            shutil.rmtree(d, ignore_errors=True)
        d.mkdir(parents=True)

    # AndroidManifest
    manifest = f'''<?xml version='1.0' encoding='utf-8'?>
<manifest xmlns:android='http://schemas.android.com/apk/res/android'
    package='com.example.app'
    android:versionCode='{version_code}'
    android:versionName='{version}'>
    <uses-sdk android:minSdkVersion='21' android:targetSdkVersion='34' />
    <uses-permission android:name='android.permission.INTERNET' />
    <application
        android:label='uni-app'
        android:icon='@drawable/icon'
        android:allowBackup='true'
        android:usesCleartextTraffic='true'>
        <activity android:name='.MainActivity'
            android:configChanges='orientation|keyboardHidden|screenSize'
            android:launchMode='singleTask'>
            <intent-filter>
                <action android:name='android.intent.action.MAIN'/>
                <category android:name='android.intent.category.LAUNCHER'/>
            </intent-filter>
        </activity>
    </application>
</manifest>'''
    (unaligned / 'AndroidManifest.xml').write_text(manifest, encoding='utf-8')

    # 资源
    res = unaligned / 'res'
    (res / 'values').mkdir(parents=True)
    (res / 'drawable').mkdir(parents=True)
    (res / 'mipmap-hdpi').mkdir(parents=True)
    (res / 'values' / 'strings.xml').write_text(
        '<?xml version="1.0" encoding="utf-8"?><resources><string name="app_name">uni-app</string></resources>',
        encoding='utf-8')

    # icon（从 wgt 提取）
    icon_src = wgt_dir / 'static' / 'logo.png'
    if icon_src.exists():
        icon_bytes = icon_src.read_bytes()
        (res / 'drawable' / 'icon.png').write_bytes(icon_bytes)
        (res / 'mipmap-hdpi' / 'icon.png').write_bytes(icon_bytes)
        print(f'  Icon: {len(icon_bytes)} bytes from wgt/static/logo.png')

    # 嵌入 wgt
    app_dir = unaligned / 'assets' / 'apps' / app_id / 'www'
    app_dir.mkdir(parents=True, exist_ok=True)
    shutil.copytree(wgt_dir, app_dir, dirs_exist_ok=True)
    print(f'  wgt embedded: {sum(1 for _ in app_dir.rglob("*") if _.is_file())} files')

    # aapt2 compile
    flat = temp_dir / 'flat'
    if flat.exists():
        shutil.rmtree(flat, ignore_errors=True)
    flat.mkdir(parents=True)
    run(f'"{tools["aapt2"]}" compile --dir "{res}" -o "{flat}"')
    compiled = ' '.join(f'"{f}"' for f in sorted(flat.rglob('*.flat')))
    proto = temp_dir / 'proto.apk'
    run(
        f'"{tools["aapt2"]}" link -o "{proto}" '
        f'-I "{tools["android_jar"]}" '
        f'--manifest "{unaligned / "AndroidManifest.xml"}" '
        f'--target-sdk-version 34 --min-sdk-version 21 '
        f'--version-code {version_code} --version-name {version} '
        f'{compiled}'
    )

    # 合并 proto + assets
    proto_ext = temp_dir / 'proto-ext'
    proto_ext.mkdir(parents=True)
    with zipfile.ZipFile(proto, 'r') as z:
        z.extractall(proto_ext)
    wgt_assets_src = unaligned / 'assets'
    wgt_assets_dst = proto_ext / 'assets'
    if wgt_assets_src.exists():
        shutil.copytree(wgt_assets_src, wgt_assets_dst, dirs_exist_ok=True)

    unsigned = temp_dir / 'unsigned.apk'
    with zipfile.ZipFile(unsigned, 'w', compression=zipfile.ZIP_DEFLATED) as zf:
        for root, _, files in os.walk(proto_ext):
            for file in files:
                fp = Path(root) / file
                zf.write(fp, fp.relative_to(proto_ext))

    # zipalign + sign
    aligned_apk = aligned / 'apk-aligned.apk'
    run(f'"{tools["zipalign"]}" -f -p 4 "{unsigned}" "{aligned_apk}"')
    ks = temp_dir / 'app-release.keystore'
    if not ks.exists():
        run(f'"{tools["keytool"]}" -genkey -v -keystore "{ks}" -alias app '
            f'-keyalg RSA -keysize 2048 -validity 10000 '
            f'-storepass android -keypass android '
            f'-dname "CN=App,O=Company,L=City,ST=State,C=CN"')
    signed_apk = signed / 'apk-signed.apk'
    run(
        f'"{tools["apksigner"]}" sign --ks "{ks}" '
        f'--ks-pass pass:android --key-pass pass:android '
        f'--ks-key-alias app --out "{signed_apk}" "{aligned_apk}"'
    )
    run(f'"{tools["apksigner"]}" verify -v "{signed_apk}"', check=False)
    return signed_apk


def main():
    parser = argparse.ArgumentParser(description='uni-app APK 构建')
    parser.add_argument('--wgt-source', type=str, required=True,
                        help='uni-app 编译产物目录（dist/build/app）')
    parser.add_argument('--version', type=str, default='1.0.0',
                        help='APK 版本号（显示在手机上）')
    parser.add_argument('--version-code', type=str, default='1',
                        help='APK versionCode（Android 必须是整数）')
    parser.add_argument('--app-id', type=str, default='',
                        help='appId（留空则从 manifest.json 自动读取）')
    parser.add_argument('--output', type=str, default='publish/latest/UniApp',
                        help='APK 输出目录')
    parser.add_argument('--android-sdk', type=str, default='',
                        help='Android SDK 路径（留空自动检测）')
    parser.add_argument('--jdk-home', type=str, default='',
                        help='JDK 路径（留空自动检测）')
    parser.add_argument('--no-base-apk', action='store_true',
                        help='跳过基座 APK，使用 fallback 从零构建（APK 约 1-2MB）')
    parser.add_argument('--keystore-pass', type=str, default='android',
                        help='keystore 密码')
    parser.add_argument('--alias', type=str, default='app',
                        help='签名 alias')
    parser.add_argument('--app-name', type=str, default='uni-app',
                        help='APP 显示名称')
    parser.add_argument('--package-name', type=str, default='com.example.app',
                        help='Android package name')
    parser.add_argument('--company', type=str, default='Example Co.',
                        help='签名 DN 公司名')
    args = parser.parse_args()

    # 路径
    wgt_dir = Path(args.wgt_source)
    if not wgt_dir.exists():
        print(f'ERROR: wgt-source 不存在: {wgt_dir}')
        return 1

    output_dir = Path(args.output)
    project_root = wgt_dir.resolve().parents[3]   # 向上 3 层到项目根

    # 工具链
    if args.android_sdk:
        os.environ['ANDROID_HOME'] = args.android_sdk
    if args.jdk_home:
        os.environ['JAVA_HOME'] = args.jdk_home
    tools = resolve_tool_paths()

    for name, path in tools.items():
        status = '[OK]' if path.exists() else '[MISSING]'
        print(f'  {status} {name}: {path}')

    # appId
    app_id = args.app_id
    if not app_id:
        app_id, wgt_ver = get_wgt_info(wgt_dir)
        print(f'\n  appId: {app_id}, wgt version: {wgt_ver}')

    temp_dir = Path(os.environ.get('TEMP', 'C:/Users/Mecall/AppData/Local/Temp')) / \
               f'uniapp-apk-build-{datetime.now().strftime("%Y%m%d_%H%M%S")}'
    temp_dir.mkdir(parents=True, exist_ok=True)

    # 基座策略
    local_base = Path('D:/Program Files/HBuilderX/plugins/launcher/base/android_base.apk')
    dcloud_url = 'https://nativesupport.dcloud.net.cn/Android/studio/zip/5.0.173457.apk'
    base_apk_path = temp_dir / 'base.apk'
    use_base = not args.no_base_apk

    if use_base and local_base.exists():
        print(f'\n[基座] 使用本地 HBuilderX: {local_base.stat().st_size / 1024 / 1024:.1f} MB')
        extract_apk(local_base, temp_dir / 'base-unpacked')
        base_dir = temp_dir / 'base-unpacked'
    elif use_base and download_base_apk(dcloud_url, base_apk_path):
        extract_apk(base_apk_path, temp_dir / 'base-unpacked')
        base_dir = temp_dir / 'base-unpacked'
    else:
        print('\n[基座] 使用 fallback 从零构建...')
        signed = build_from_scratch(wgt_dir, app_id, args.version, args.version_code,
                                    tools, temp_dir)
        output_dir.mkdir(parents=True, exist_ok=True)
        dest = output_dir / f'{app_id}-{args.version}.apk'
        shutil.copy2(signed, dest)
        print(f'\n[DONE] APK: {dest} ({dest.stat().st_size / 1024 / 1024:.2f} MB)')
        shutil.rmtree(temp_dir, ignore_errors=True)
        return 0

    # 替换 wgt
    print('\n[替换] 嵌入 wgt 资源...')
    apps_dir = base_dir / 'assets' / 'apps' / app_id
    if apps_dir.exists():
        shutil.rmtree(apps_dir)
    www_dest = apps_dir / 'www'
    www_dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copytree(wgt_dir, www_dest)
    wgt_count = sum(1 for _ in www_dest.rglob('*') if _.is_file())
    print(f'  Replaced: {wgt_count} files')

    # keystore
    ks = temp_dir / 'app-release.keystore'
    if not ks.exists():
        print('\n[签名] 生成 keystore...')
        run(f'"{tools["keytool"]}" -genkey -v -keystore "{ks}" -alias {args.alias} '
            f'-keyalg RSA -keysize 2048 -validity 10000 '
            f'-storepass {args.keystore_pass} -keypass {args.keystore_pass} '
            f'-dname "CN={args.company}"')

    # 重打包
    print('\n[打包] 重新打包为 APK...')
    unsigned = temp_dir / 'unsigned.apk'
    with zipfile.ZipFile(unsigned, 'w', compression=zipfile.ZIP_DEFLATED) as zf:
        for root, _, files in os.walk(base_dir):
            for file in files:
                fp = Path(root) / file
                zf.write(fp, fp.relative_to(base_dir))

    # zipalign
    aligned = temp_dir / 'aligned.apk'
    run(f'"{tools["zipalign"]}" -f -p 4 "{unsigned}" "{aligned}"')

    # 签名
    print('\n[签名] apksigner sign...')
    signed = temp_dir / 'signed.apk'
    run(
        f'"{tools["apksigner"]}" sign --ks "{ks}" '
        f'--ks-pass pass:{args.keystore_pass} --key-pass pass:{args.keystore_pass} '
        f'--ks-key-alias {args.alias} --out "{signed}" "{aligned}"'
    )
    run(f'"{tools["apksigner"]}" verify -v "{signed}"', check=False)

    # 部署
    print('\n[部署] 复制到输出目录...')
    output_dir.mkdir(parents=True, exist_ok=True)
    dest = output_dir / f'{app_id}-{args.version}.apk'
    shutil.copy2(signed, dest)

    # 归档
    archive_dir = project_root / 'publish' / '_archive'
    archive_dir.mkdir(parents=True, exist_ok=True)
    ts = datetime.now().strftime('%Y%m%d_%H%M%S')
    archive = archive_dir / f'{app_id}-{args.version}_{ts}.apk'
    shutil.copy2(signed, archive)

    # 清理
    shutil.rmtree(temp_dir, ignore_errors=True)

    size_mb = dest.stat().st_size / 1024 / 1024
    print()
    print('=' * 60)
    print(f'  [DONE] APK: {dest.name} ({size_mb:.2f} MB)')
    print(f'  [DONE] Archive: {archive.name}')
    print('=' * 60)
    return 0


if __name__ == '__main__':
    import sys
    sys.exit(main())
