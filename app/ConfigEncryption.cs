using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AccountingETL.App;

/// <summary>
/// เข้ารหัส/ถอดรหัสไฟล์ config.json ด้วย AES-256
/// ใช้ DPAPI (Windows) เป็น key derivation → ไม่ต้องเก็บ key
/// </summary>
public static class ConfigEncryption
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ExpressETL-Config-Entropy-2026");

    /// <summary>
    /// เข้ารหัส config เป็น JSON string แล้ว encrypt
    /// </summary>
    public static string Encrypt(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = false });
        var plainBytes = Encoding.UTF8.GetBytes(json);

        // AES-256 encryption with DPAPI-protected key
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.GenerateKey();
        aes.GenerateIV();

        // Protect the AES key using Windows DPAPI
        var protectedKey = ProtectedData.Protect(aes.Key, Entropy, DataProtectionScope.CurrentUser);

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Package: [IV(16)] + [protectedKeyLen(4)] + [protectedKey] + [cipherText]
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(aes.IV);
        bw.Write(protectedKey.Length);
        bw.Write(protectedKey);
        bw.Write(cipherBytes);

        // Return as base64
        return Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>
    /// ถอดรหัสจาก encrypted string กลับเป็น AppConfig
    /// </summary>
    public static AppConfig? Decrypt(string encrypted)
    {
        try
        {
            var data = Convert.FromBase64String(encrypted);
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            var iv = br.ReadBytes(16);
            var keyLen = br.ReadInt32();
            var protectedKey = br.ReadBytes(keyLen);
            var cipherBytes = br.ReadBytes((int)(data.Length - 16 - 4 - keyLen));

            // Unprotect the AES key using DPAPI
            var aesKey = ProtectedData.Unprotect(protectedKey, Entropy, DataProtectionScope.CurrentUser);

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Key = aesKey;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

            var json = Encoding.UTF8.GetString(plainBytes);
            return JsonSerializer.Deserialize<AppConfig>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// ตรวจสอบว่า string เป็น encrypted config หรือยัง (base64 ที่ decode แล้วขึ้นต้นด้วย IV)
    /// </summary>
    public static bool IsEncrypted(string content)
    {
        // Encrypted config is base64 and decodes to >50 bytes
        // Plain JSON starts with '{'
        if (content.TrimStart().StartsWith('{')) return false;
        try
        {
            var decoded = Convert.FromBase64String(content.Trim());
            return decoded.Length > 50; // Minimum: IV(16) + keyLen(4) + minKey(32) + someCipher
        }
        catch
        {
            return false;
        }
    }
}
