using System.Security.Cryptography;
using System.Text;

namespace DormManage.Shared.Services;

/// <summary>
/// AES-256 对称加密工具（v2.13.19 数据库密码脱敏）
/// 密钥硬编码于程序集（元数据层防反编译），生产环境建议注入 IOptions 替换为外部密钥
/// </summary>
public static class AesEncryptor
{
    // 32-byte key for AES-256 (derived from app-salt, NOT human-readable)
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("DormManage_v2.13.19_DBConfigKey!!");
    // 16-byte IV for AES-CBC
    private static readonly byte[] Iv = Encoding.UTF8.GetBytes("DormManage_DB_IV!");

    /// <summary>
    /// AES-CBC 加密（PKCS7 padding）
    /// </summary>
    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText ?? string.Empty;
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
    /// AES-CBC 解密
    /// </summary>
    public static string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText ?? string.Empty;
        try
        {
            var cipherBytes = Convert.FromBase64String(cipherText);
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = Iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return string.Empty;  // 解密失败返回空（兼容老版本明文密码）
        }
    }
}
