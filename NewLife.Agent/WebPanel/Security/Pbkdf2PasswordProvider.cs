#if !NET40
using System.Globalization;
using System.Security.Cryptography;
using NewLife.Security;

namespace NewLife.Agent.WebPanel.Security;

/// <summary>PBKDF2-SHA256 口令提供者</summary>
/// <remarks>
/// 与 <see cref="SaltPasswordProvider"/> 的单轮加盐散列相比，PBKDF2 通过高迭代次数抬高离线爆破成本，
/// 用于缓解配置文件泄露后弱口令被快速破解的风险。
/// 兼容历史格式：明文、MD5、$sha1$/$sha512$/... 等 NewLife 旧哈希仍可验证，登录成功后由面板自动升级为 PBKDF2。
/// </remarks>
public class Pbkdf2PasswordProvider : IPasswordProvider
{
    #region 属性
    /// <summary>哈希前缀标识</summary>
    public const String Prefix = "$pbkdf2-sha256$";

    /// <summary>迭代次数。默认210000，可按服务器算力调整</summary>
    public Int32 Iterations { get; set; } = 210_000;

    /// <summary>盐值字节数。默认16</summary>
    public Int32 SaltSize { get; set; } = 16;

    /// <summary>哈希字节数。默认32</summary>
    public Int32 HashSize { get; set; } = 32;

    private static readonly SaltPasswordProvider _legacy = new();
    #endregion

    #region 方法
    /// <summary>对密码进行散列处理</summary>
    /// <param name="password">密码明文</param>
    /// <returns>形如 $pbkdf2-sha256$迭代次数$盐$哈希 的密文</returns>
    public String Hash(String password)
    {
        if (password == null) throw new ArgumentNullException(nameof(password));

        var salt = Rand.NextBytes(SaltSize);
        var hash = Derive(password.GetBytes(), salt, Iterations, HashSize);

        return $"{Prefix}{Iterations}${salt.ToBase64()}${hash.ToBase64()}";
    }

    /// <summary>验证密码散列，兼容历史明文和旧哈希格式</summary>
    /// <param name="password">密码明文</param>
    /// <param name="hash">哈希密文</param>
    /// <returns>是否匹配</returns>
    public Boolean Verify(String password, String hash)
    {
        if (password == null || hash.IsNullOrEmpty()) return false;

        if (!IsPbkdf2Hash(hash)) return _legacy.Verify(password, hash);

        var parts = hash.Split('$');
        if (parts.Length != 5) return false;
        if (!Int32.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) || iterations <= 0) return false;

        Byte[] salt;
        Byte[] expected;
        try
        {
            salt = parts[3].ToBase64();
            expected = parts[4].ToBase64();
        }
        catch
        {
            return false;
        }

        if (salt.Length == 0 || expected.Length == 0) return false;

        var actual = Derive(password.GetBytes(), salt, iterations, expected.Length);

        return FixedTimeEquals(actual, expected);
    }

    /// <summary>判断是否为 PBKDF2 哈希</summary>
    /// <param name="hash">哈希密文</param>
    /// <returns>是否为当前格式</returns>
    public static Boolean IsPbkdf2Hash(String hash) => !hash.IsNullOrEmpty() && hash.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>PBKDF2-HMAC-SHA256 派生</summary>
    /// <param name="password">密码字节</param>
    /// <param name="salt">盐值</param>
    /// <param name="iterations">迭代次数</param>
    /// <param name="length">输出长度</param>
    /// <returns>派生密钥</returns>
    internal static Byte[] Derive(Byte[] password, Byte[] salt, Int32 iterations, Int32 length)
    {
        using var hmac = new HMACSHA256(password);

        var output = new Byte[length];
        var block = new Byte[4];
        var data = new Byte[salt.Length + 4];
        Buffer.BlockCopy(salt, 0, data, 0, salt.Length);

        var offset = 0;
        for (var i = 1; offset < length; i++)
        {
            block[0] = (Byte)(i >> 24);
            block[1] = (Byte)(i >> 16);
            block[2] = (Byte)(i >> 8);
            block[3] = (Byte)i;
            Buffer.BlockCopy(block, 0, data, salt.Length, 4);

            var u = hmac.ComputeHash(data);
            var t = (Byte[])u.Clone();
            for (var j = 1; j < iterations; j++)
            {
                u = hmac.ComputeHash(u);
                for (var k = 0; k < t.Length; k++) t[k] ^= u[k];
            }

            var count = Math.Min(t.Length, length - offset);
            Buffer.BlockCopy(t, 0, output, offset, count);
            offset += count;
        }

        return output;
    }

    /// <summary>恒定时间比较，避免时序侧信道</summary>
    private static Boolean FixedTimeEquals(Byte[] left, Byte[] right)
    {
        if (left.Length != right.Length) return false;

        var diff = 0;
        for (var i = 0; i < left.Length; i++) diff |= left[i] ^ right[i];

        return diff == 0;
    }
    #endregion
}
#endif
