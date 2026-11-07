#if !NET40
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
#if NET6_0_OR_GREATER
using System.Net;
#endif
using NewLife.Configuration;
using NewLife.Log;

namespace NewLife.Agent.WebPanel.Security;

/// <summary>Web面板安全辅助。配置/证书文件权限收紧与自签证书生成</summary>
public static class WebPanelSecurity
{
    #region 配置文件
    /// <summary>获取配置文件所在目录</summary>
    /// <returns>目录绝对路径；无法解析时返回程序目录</returns>
    public static String GetConfigDirectory()
    {
        if (Setting.Provider is FileConfigProvider provider && !provider.FileName.IsNullOrEmpty())
        {
            var path = provider.FileName.GetBasePath();
            var dir = Path.GetDirectoryName(path);
            if (!dir.IsNullOrEmpty()) return dir;
        }

        return AppDomain.CurrentDomain.BaseDirectory;
    }

    /// <summary>收紧文件权限，避免同机低权限用户读取保存的凭据</summary>
    /// <param name="file">文件全路径</param>
    public static void ProtectFile(String file)
    {
        if (file.IsNullOrEmpty() || !File.Exists(file)) return;

        try
        {
            if (Runtime.Windows)
                RestrictWindowsFile(file);
            else
                ChmodFile(file, 0x180); // 0600
        }
        catch (Exception ex)
        {
            XTrace.Log.WriteLogPrefixed(LogLevel.Warn, "[WebPanel]", "Failed to tighten the permission of {0}: {1}", file, ex.Message);
        }
    }

    /// <summary>收紧面板配置文件的权限</summary>
    public static void ProtectConfigFile()
    {
        if (Setting.Provider is not FileConfigProvider provider || provider.FileName.IsNullOrEmpty()) return;

        ProtectFile(provider.FileName.GetBasePath());
    }

    private static void RestrictWindowsFile(String file)
    {
        // 使用 SID 授权，兼容非英文系统；* 前缀表示按 SID 解析。
        // 必须同时保留当前运行账号的权限，否则非 SYSTEM 运行时会读不回自己的配置
        var identity = Environment.UserName;
        if (!Environment.UserDomainName.IsNullOrEmpty()) identity = Environment.UserDomainName + "\\" + identity;

        var psi = new ProcessStartInfo
        {
            FileName = "icacls",
            Arguments = $"\"{file}\" /inheritance:r /grant:r *S-1-5-18:F *S-1-5-32-544:F \"{identity}\":F",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi);
        if (process == null) return;

        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit(5000);
    }

    // Unix 上 ANSI 编组即为 UTF-8，兼容低版本目标框架（LPUTF8Str 仅新框架提供）
    [DllImport("libc", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern Int32 chmod(String path, Int32 mode);

    private static void ChmodFile(String file, Int32 mode) => chmod(file, mode);
    #endregion

    #region 自签证书
    /// <summary>加载或生成面板自签证书</summary>
    /// <param name="file">PFX 文件路径</param>
    /// <param name="password">PFX 保护口令</param>
    /// <param name="allowCreate">文件不存在或无法加载时是否允许生成自签证书。用户提供的证书必须传 false，避免覆盖</param>
    /// <returns>证书；无法加载且不允许生成时返回 null</returns>
    public static X509Certificate CreateSelfSigned(String file, String password, Boolean allowCreate = true)
    {
        if (file.IsNullOrEmpty()) return null;

        // 已存在的证书在任意目标框架下都可以加载，保证用户提供的 PFX 可用
        if (File.Exists(file))
        {
            try
            {
#if NET9_0_OR_GREATER
                return X509CertificateLoader.LoadPkcs12FromFile(file, password);
#else
                return new X509Certificate2(file, password);
#endif
            }
            catch (Exception ex) when (ex is CryptographicException || ex is IOException)
            {
                if (!allowCreate)
                {
                    XTrace.Log.WriteLogPrefixed(LogLevel.Error, "[WebPanel]", "Failed to load the panel certificate {0}: {1}", file, ex.Message);

                    return null;
                }

                XTrace.Log.WriteLogPrefixed(LogLevel.Warn, "[WebPanel]", "Failed to load the panel certificate {0}, regenerating: {1}", file, ex.Message);
            }
        }
        else if (!allowCreate)
        {
            XTrace.Log.WriteLogPrefixed(LogLevel.Error, "[WebPanel]", "The panel certificate {0} does not exist.", file);

            return null;
        }

#if NET6_0_OR_GREATER
        try
        {
            var host = Environment.MachineName;

            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest($"CN={host}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            var san = new SubjectAlternativeNameBuilder();
            san.AddDnsName(host);
            san.AddDnsName("localhost");
            san.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(san.Build());

            var usage = new OidCollection { new("1.3.6.1.5.5.7.3.1") };
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usage, false));

            using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));

            // 保存为 PFX，保证重启后浏览器看到的指纹不变
            var bytes = cert.Export(X509ContentType.Pfx, password);
            file.EnsureDirectory(true);
            File.WriteAllBytes(file, bytes);
            ProtectFile(file);

#if NET9_0_OR_GREATER
            return X509CertificateLoader.LoadPkcs12(bytes, password);
#else
            return new X509Certificate2(bytes, password);
#endif
        }
        catch (Exception ex)
        {
            XTrace.Log.WriteLogPrefixed(LogLevel.Warn, "[WebPanel]", "Failed to generate a self-signed certificate: {0}", ex.Message);

            return null;
        }
#else
        XTrace.Log.WriteLogPrefixed(LogLevel.Warn, "[WebPanel]", "Self-signed certificates are not supported on this runtime; configure a PFX certificate or allow insecure remote access explicitly.");

        return null;
#endif
    }
    #endregion
}
#endif
