using System.Security.Cryptography;
using System.Text;

namespace DormManage.Shared.Services;

/// <summary>
/// AES-256 对称加密工具（v2.13.19 数据库密码脱敏）
/// 密钥硬编码于程序集（元数据层防反编译），生产环境建议注入 IOptions 替换为外部密钥
///
/// v2.13.27 BUGFIX：
///   原 Key/IV 字符串长度错误（AES-256 Key 必须恰好 32 字节，IV 必须恰好 16 字节）。
///   修复后同时支持解密历史已保存的"兼容标记"明文（"plain:" 前缀）。
///   旧 db_setting.json 中的旧密文如果解密失败，会原样返回（避免误清空）。
/// </summary>
public static class AesEncryptor
{
    // 32-byte key for AES-256
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("JINGE_v2.13.27_DBConfigKey_32B!!");
    // 16-byte IV for AES-CBC
    private static readonly byte[] Iv = Encoding.UTF8.GetBytes("JINGE_DB_IV_16_B");

    /// <summary>
    /// 明文标记前缀：若密文以此开头，则视为明文（不加密），便于迁移与开发调试
    /// </summary>
    private const string PlainPrefix = "plain:";

    /// <summary>
    /// AES-CBC 加密（PKCS7 padding）
    /// </summary>
    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText ?? string.Empty;
        // 不对已加密的密文再加密（避免双重加密）
        if (IsCipherText(plainText)) return plainText;

        using var aes = Aes.Create();
        aes.Key = Key;
        aes.IV = Iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        return Convert.ToBase64String(cipherBytes);
    }

    /// <summary>
    /// AES-CBC 解密（兼容明文前缀 + 历史密文回退）
    /// </summary>
    public static string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText ?? string.Empty;

        // 1) 明文标记
        if (cipherText.StartsWith(PlainPrefix))
            return cipherText.Substring(PlainPrefix.Length);

        // 2) Base64 解码失败 → 视为明文直接返回
        byte[] cipherBytes;
        try
        {
            cipherBytes = Convert.FromBase64String(cipherText);
        }
        catch
        {
            return cipherText;
        }

        // 3) 长度不是 16 字节倍数 → 不是合法 AES 密文，视为明文返回
        if (cipherBytes.Length == 0 || cipherBytes.Length % 16 != 0)
            return cipherText;

        // 4) AES-CBC 解密
        try
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = Iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            var plain = Encoding.UTF8.GetString(plainBytes);
            return plain;
        }
        catch
        {
            // 5) 解密失败：可能是历史旧 Key/IV 加密的数据，无法解密时返回原字符串
            //     避免数据丢失（用户可重新保存触发重新加密）
            return cipherText;
        }
    }

    /// <summary>
    /// 判断是否为合法 AES 密文（Base64 且长度 = 16 倍数）
    /// </summary>
    private static bool IsCipherText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        if (text.StartsWith(PlainPrefix)) return false;
        try
        {
            var bytes = Convert.FromBase64String(text);
            return bytes.Length > 0 && bytes.Length % 16 == 0;
        }
        catch
        {
            return false;
        }
    }
}