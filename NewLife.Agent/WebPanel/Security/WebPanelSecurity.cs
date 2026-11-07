#if !NET40
using System.Diagnostics;
using System.Runtime.InteropServices;
using NewLife.Configuration;
using NewLife.Log;

namespace NewLife.Agent.WebPanel.Security;

/// <summary>Web面板安全辅助。配置文件权限收紧</summary>
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
}
#endif
