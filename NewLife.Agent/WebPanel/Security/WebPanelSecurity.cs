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
    /// <summary>收紧文件权限，避免同机低权限用户读取保存的凭据</summary>
    /// <param name="file">文件全路径</param>
    public static void ProtectFile(String file)
    {
        if (file.IsNullOrEmpty() || !File.Exists(file)) return;

        try
        {
            if (!TryProtectFile(file))
                XTrace.Log.WriteLogPrefixed(LogLevel.Warn, "[WebPanel]", "Failed to tighten the permission of {0}.", file);
        }
        catch (Exception ex)
        {
            XTrace.Log.WriteLogPrefixed(LogLevel.Warn, "[WebPanel]", "Failed to tighten the permission of {0}: {1}", file, ex.Message);
        }
    }

    /// <summary>收紧文件权限，失败时返回 false</summary>
    private static Boolean TryProtectFile(String file)
    {
        if (Runtime.Windows)
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
            if (process == null) return false;

            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();

            return process.WaitForExit(5000) && process.ExitCode == 0;
        }

        return ChmodFile(file, 0x180); // 0600
    }

    /// <summary>收紧面板配置文件的权限</summary>
    public static void ProtectConfigFile()
    {
        if (Setting.Provider is not FileConfigProvider provider || provider.FileName.IsNullOrEmpty()) return;

        ProtectFile(provider.FileName.GetBasePath());
    }

    // Unix 上 ANSI 编组即为 UTF-8，兼容低版本目标框架（LPUTF8Str 仅新框架提供）
    [DllImport("libc", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern Int32 chmod(String path, Int32 mode);

    private static Boolean ChmodFile(String file, Int32 mode) => chmod(file, mode) == 0;
    #endregion
}
#endif
