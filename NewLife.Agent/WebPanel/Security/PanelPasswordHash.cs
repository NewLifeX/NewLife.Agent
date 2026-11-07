#if !NET40
using System.Security.Cryptography;
using System.Text;

namespace NewLife.Agent.WebPanel.Security;

/// <summary>面板口令哈希器</summary>
/// <remarks>
/// 面板口令不再以明文落盘，统一保存为 <c>PBKDF2$迭代次数$盐$哈希</c> 编码串。
/// 历史配置中的明文口令仍可验证，并在首次登录成功后自动升级为哈希。
/// </remarks>
public static class PanelPasswordHash
{
    #region 属性
    /// <summary>哈希前缀。用于识别编码串与历史明文</summary>
    public const String Prefix = "PBKDF2";

    /// <summary>迭代次数。默认10万次，兼顾安全与登录耗时</summary>
    public static Int32 Iterations { get; set; } = 100_000;

    /// <summary>盐长度（字节）。默认16</summary>
    public static Int32 SaltSize { get; set; } = 16;

    /// <summary>哈希长度（字节）。默认32</summary>
    public static Int32 HashSize { get; set; } = 32;

    /// <summary>随机口令字符集。去掉易混淆的 0/O/1/l/I</summary>
    private const String PasswordChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
    #endregion

    #region 方法
    /// <summary>判断给定值是否为哈希编码串</summary>
    /// <param name="value">待判断的值</param>
    /// <returns>true 表示已是哈希串；false 表示可能是历史明文</returns>
    public static Boolean IsHash(String value) =>
        !value.IsNullOrEmpty() && value.StartsWith(Prefix + "$", StringComparison.Ordinal);

    /// <summary>判断给定值是否需要升级为哈希</summary>
    /// <param name="value">已保存的口令值</param>
    /// <returns>true 表示仍是历史明文</returns>
    public static Boolean IsLegacy(String value) => !value.IsNullOrEmpty() && !IsHash(value);

    /// <summary>计算口令哈希</summary>
    /// <param name="password">口令明文</param>
    /// <returns>编码串 <c>PBKDF2$迭代次数$盐$哈希</c></returns>
    public static String Hash(String password)
    {
        if (password.IsNullOrEmpty()) throw new ArgumentNullException(nameof(password));

        var salt = RandomBytes(SaltSize);
        var hash = Derive(password, salt, Iterations);

        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>验证口令</summary>
    /// <param name="password">待验证的口令明文</param>
    /// <param name="stored">已保存的口令值，可能是哈希串或历史明文</param>
    /// <returns>是否匹配</returns>
    public static Boolean Verify(String password, String stored)
    {
        if (password.IsNullOrEmpty() || stored.IsNullOrEmpty()) return false;

        // 历史明文：使用定长比较，避免时序侧信道
        if (!IsHash(stored)) return FixedTimeEquals(Encoding.UTF8.GetBytes(password), Encoding.UTF8.GetBytes(stored));

        var parts = stored.Split('$');
        if (parts.Length != 4) return false;
        if (!Int32.TryParse(parts[1], out var iterations) || iterations <= 0) return false;

        Byte[] salt;
        Byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length == 0 || expected.Length == 0) return false;

        return FixedTimeEquals(Derive(password, salt, iterations), expected);
    }

    /// <summary>生成随机初始口令</summary>
    /// <param name="length">口令长度，默认16位</param>
    /// <returns>随机口令明文</returns>
    public static String CreateRandomPassword(Int32 length = 16)
    {
        if (length <= 0) length = 16;

        var bytes = RandomBytes(length);
        var chars = new Char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = PasswordChars[bytes[i] % PasswordChars.Length];
        }

        return new String(chars);
    }
    #endregion

    #region 辅助
    /// <summary>派生密钥。NET6+ 使用 PBKDF2-HMAC-SHA256，低版本回退到框架自带的 SHA1 实现</summary>
    private static Byte[] Derive(String password, Byte[] salt, Int32 iterations)
    {
#if NET6_0_OR_GREATER
        return Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashSize);
#else
        using var kdf = new Rfc2898DeriveBytes(password, salt, iterations);
        return kdf.GetBytes(HashSize);
#endif
    }

    /// <summary>生成密码学随机字节</summary>
    private static Byte[] RandomBytes(Int32 length)
    {
#if NET6_0_OR_GREATER
        return RandomNumberGenerator.GetBytes(length);
#else
        var buffer = new Byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(buffer);
        return buffer;
#endif
    }

    /// <summary>定长比较，避免因为提前返回而泄露前缀信息</summary>
    private static Boolean FixedTimeEquals(Byte[] left, Byte[] right)
    {
        if (left == null || right == null || left.Length != right.Length) return false;

        var diff = 0;
        for (var i = 0; i < left.Length; i++)
        {
            diff |= left[i] ^ right[i];
        }

        return diff == 0;
    }
    #endregion
}
#endif
