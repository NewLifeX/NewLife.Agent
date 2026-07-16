#if !NET40
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Reflection;
using NewLife.Data;
using NewLife.Http;
using NewLife.Log;
using NewLife.Net;
using NewLife.Reflection;
using NewLife.Serialization;
using System.Threading;

namespace NewLife.Agent.WebPanel;

/// <summary>Agent Web管理面板 API 控制器</summary>
/// <remarks>
/// 提供 RESTful API 接口，包括服务状态查看、启停控制、配置管理、日志查看等功能。
/// 路由格式：/api/{MethodName}，由 HttpServer.ControllerHandler 自动映射。
/// 控制器自行完成 Bearer Token 鉴权（Login 方法除外）。
/// </remarks>
public class ApiController : IHttpController
{
    #region 属性
    /// <summary>所属服务</summary>
    public ServiceBase Service => AgentWebPanel.Current?.Service!;

    /// <summary>当前Http上下文。由 ControllerHandler 自动注入</summary>
    public IHttpContext Context { get; set; }
    #endregion

    #region 鉴权
    /// <summary>登录鉴权，签发Bearer Token</summary>
    /// <param name="user">用户名</param>
    /// <param name="password">密码</param>
    /// <returns>Token信息，失败返回 code=401，爆破封锁返回 code=429</returns>
    public Object Login(String user, String password)
    {
        // 爆破防护：获取客户端IP
        var ip = GetClientIp();

        if (LoginRateLimiter.IsBlocked(ip))
            return new { code = 429, message = "Too many failed attempts. Try again later." };

        var token = AgentWebPanel.Current?.IssueToken(user, password);
        if (token == null)
        {
            LoginRateLimiter.RecordFailure(ip);
            return new { code = 401, message = "Invalid credentials" };
        }

        LoginRateLimiter.RecordSuccess(ip);
        return new { code = 0, data = new { token } };
    }

    /// <summary>注销登录，吊销当前Token</summary>
    /// <returns>操作结果</returns>
    public Object Logout()
    {
        var ctx = Context;
        var auth = ctx?.Request.Headers["Authorization"];
        if (auth.IsNullOrEmpty() || !auth.StartsWithIgnoreCase("Bearer "))
            return new { code = 0, message = "ok" };

        var token = auth.Substring("Bearer ".Length).Trim();
        if (!token.IsNullOrEmpty())
            AgentWebPanel.Current?.RevokeToken(token);

        return new { code = 0, message = "ok" };
    }

    /// <summary>检查请求鉴权</summary>
    /// <returns>是否通过鉴权</returns>
    protected Boolean CheckAuth()
    {
        var ctx = Context;
        if (ctx == null) return false;

        var auth = ctx.Request.Headers["Authorization"];
        if (auth.IsNullOrEmpty() || !auth.StartsWithIgnoreCase("Bearer "))
            return false;

        var token = auth.Substring("Bearer ".Length).Trim();
        if (token.IsNullOrEmpty()) return false;

        return AgentWebPanel.Current.ValidateToken(token);
    }

    /// <summary>获取客户端IP地址</summary>
    /// <returns>IP字符串，无法获取时返回 unknown</returns>
    protected String GetClientIp()
    {
        var ctx = Context;
        var socket = ctx?.Socket;
        if (socket?.Remote == null || socket.Remote.Address == null) return "unknown";

        return socket.Remote.Address + "";
    }
    #endregion

    #region 状态
    /// <summary>获取服务状态</summary>
    /// <returns>服务状态信息，包含运行状态、进程信息、系统资源等</returns>
    public Object Status()
    {
        if (!CheckAuth()) return new { code = 401, message = "Unauthorized" };

        var p = Process.GetCurrentProcess();
        var uptime = DateTime.Now - p.StartTime;
        var mi = MachineInfo.Current ?? MachineInfo.GetCurrent();

        // 刷新 MachineInfo 动态数据（CPU 使用率、网络速度等）
        mi.Refresh();
        mi.RefreshSpeed();

        // 收集 TCP 连接数
        var tcpConnections = 0;
        var tcpTimeWait = 0;
        var tcpCloseWait = 0;
        try
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();
            var connections = properties.GetActiveTcpConnections();

            tcpConnections = connections.Count(e => e.State == TcpState.Established);
            tcpTimeWait = connections.Count(e => e.State == TcpState.TimeWait);
            tcpCloseWait = connections.Count(e => e.State == TcpState.CloseWait);
        }
        catch { }

        // 收集磁盘 IOPS
        var diskIops = DiskMonitor.GetIOPS();

        return new
        {
            code = 0,
            data = new
            {
                serviceName = Service.ServiceName,
                displayName = Service.DisplayName,
                description = Service.Description,
                running = Service.Running,
                uptime = uptime.ToString(@"d\.hh\:mm\:ss"),
                uptimeSeconds = (Int64)uptime.TotalSeconds,
                processId = p.Id,
                memoryMB = p.WorkingSet64 / 1024 / 1024,
                // 物理内存总量
                memoryTotalMB = mi.Memory > 0 ? mi.Memory / 1024 / 1024 : 0,
                threadCount = p.Threads.Count,
                handleCount = p.HandleCount,
                startTime = p.StartTime.ToString("MM-dd HH:mm:ss"),
                hostMachine = Environment.MachineName,
                platform = mi.OSName ?? Environment.OSVersion.Platform.ToString(),
                osVersion = mi.OSVersion,
                cpuName = mi.Processor,
                cpuCount = Environment.ProcessorCount,
                // CPU 使用率百分比（0~100），保留一位小数
                cpuRate = mi.CpuRate > 0 ? mi.CpuRate.ToString("F1") : "",
                cpuRateValue = mi.CpuRate > 0 ? Math.Round(mi.CpuRate, 1) : 0d,
                totalMemory = mi.Memory > 0 ? $"{mi.Memory / 1024 / 1024 / 1024} GB" : "",
                availableMemory = mi.AvailableMemory > 0 ? $"{mi.AvailableMemory / 1024 / 1024 / 1024} GB" : "",
                freeMemory = mi.FreeMemory > 0 ? $"{mi.FreeMemory / 1024 / 1024 / 1024} GB" : "",
                board = mi.Board,
                machineGuid = mi.Guid,
                uplinkSpeed = mi.UplinkSpeed > 0 ? FormatSpeed(mi.UplinkSpeed) : "",
                downlinkSpeed = mi.DownlinkSpeed > 0 ? FormatSpeed(mi.DownlinkSpeed) : "",
                // TCP 连接数
                tcpConnections,
                tcpTimeWait,
                tcpCloseWait,
                // 磁盘 IOPS
                diskIops,
                hostUptime = TimeSpan.FromMilliseconds(Runtime.TickCount64).ToString(@"d\.hh\:mm\:ss"),
                port = AgentWebPanel.Current?.Server.Port ?? 0
            }
        };
    }
    #endregion

    #region 控制
    /// <summary>服务控制（启停重启）</summary>
    /// <param name="action">操作类型：start/stop/restart</param>
    /// <returns>操作结果</returns>
    public Object Control(String action)
    {
        if (!CheckAuth()) return new { code = 401, message = "Unauthorized" };

        if (action.IsNullOrEmpty())
            return new { code = 400, message = "Missing action" };

        switch (action.ToLower())
        {
            case "stop":
                XTrace.WriteLine("Web面板触发服务停止");
                Service.Running = false;
                return new { code = 0, message = "服务正在停止，Web面板仍可用" };

            case "start":
                XTrace.WriteLine("Web面板触发服务启动");
                Service.Host.Restart(Service.ServiceName);
                return new { code = 0, message = "服务正在重启" };

            case "restart":
                XTrace.WriteLine("Web面板触发服务重启");
                Service.Host.Restart(Service.ServiceName);
                return new { code = 0, message = "服务正在重启" };

            default:
                return new { code = 400, message = $"Unknown action: {action}" };
        }
    }
    #endregion

    #region 配置
    /// <summary>获取配置元数据（DisplayName + Description + Type + Value），供前端三列布局渲染</summary>
    /// <returns>配置项列表，含显示名、描述、类型和当前值</returns>
    public Object ConfigMetadata()
    {
        if (!CheckAuth()) return new { code = 401, message = "Unauthorized" };

        var set = Setting.Current;
        var items = new List<Object>();

        foreach (var prop in typeof(Setting).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || !prop.CanWrite) continue;
            if (prop.Name.EqualIgnoreCase(nameof(set.WebPassword))) continue;

            var displayName = prop.GetCustomAttribute<System.ComponentModel.DisplayNameAttribute>()?.DisplayName;
            if (displayName.IsNullOrEmpty())
            {
                var desc = prop.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description;
                if (!desc.IsNullOrEmpty())
                {
                    var dot = desc.IndexOf('。');
                    displayName = dot > 0 ? desc[..dot] : desc;
                }
            }
            displayName ??= prop.Name;

            var description = prop.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description ?? "";

            var typeName = prop.PropertyType.Name switch
            {
                "String" => "String",
                "Int32" => "Int32",
                "Boolean" => "Boolean",
                _ => prop.PropertyType.Name
            };

            items.Add(new
            {
                name = prop.Name,
                displayName,
                description,
                type = typeName,
                value = prop.GetValue(set, null)
            });
        }

        return new { code = 0, data = new { items } };
    }

    /// <summary>更新配置</summary>
    /// <remarks>
    /// 接收 JSON 对象，key 为属性名，value 为新值。
    /// 安全字段白名单：不更新密码字段（请使用 ChangePassword 接口）。
    /// </remarks>
    /// <returns>更新结果</returns>
    public Object UpdateConfig(IDictionary<String, Object> updates)
    {
        if (!CheckAuth()) return new { code = 401, message = "Unauthorized" };

        if (updates == null || updates.Count == 0)
            return new { code = 400, message = "Missing config" };

        try
        {
            var set = Setting.Current;

            foreach (var kv in updates)
            {
                var prop = typeof(Setting).GetProperty(kv.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null || !prop.CanWrite) continue;

                // 安全字段白名单：密码须走 ChangePassword 接口（校验旧密码）
                if (kv.Key.EqualIgnoreCase(nameof(set.WebPassword))) continue;

                var value = kv.Value;
                if (value != null && value.GetType() != prop.PropertyType)
                    value = Reflect.ChangeType(value, prop.PropertyType);

                prop.SetValue(set, value, null);
            }

            set.Save();

            return new { code = 0, message = "配置已更新，部分配置需重启服务后生效" };
        }
        catch (Exception ex)
        {
            return new { code = 500, message = ex.Message };
        }
    }

    /// <summary>修改密码。校验旧密码后更新为新密码，立即生效无需重启</summary>
    /// <param name="oldPassword">旧密码</param>
    /// <param name="newPassword">新密码</param>
    /// <returns>操作结果</returns>
    public Object ChangePassword(String oldPassword, String newPassword)
    {
        if (!CheckAuth()) return new { code = 401, message = "Unauthorized" };

        if (oldPassword.IsNullOrEmpty()) return new { code = 400, message = "Missing oldPassword" };
        if (newPassword.IsNullOrEmpty()) return new { code = 400, message = "Missing newPassword" };

        var panel = AgentWebPanel.Current;
        if (panel == null) return new { code = 500, message = "Panel not available" };

        // 验证旧密码
        if (oldPassword != panel.Password)
            return new { code = 403, message = "Old password is incorrect" };

        // 更新配置文件
        var set = Setting.Current;
        set.WebPassword = newPassword;
        set.Save();

        // 更新面板内存中的密码（立即生效，无需重启）
        panel.UpdateCredentials(null, newPassword);

        XTrace.WriteLine("Web面板密码已修改");

        return new { code = 0, message = "密码已修改，下次登录请使用新密码" };
    }
    #endregion

    #region 日志
    /// <summary>获取日志内容</summary>
    /// <param name="count">读取行数，默认200，最大1000</param>
    /// <param name="file">指定日志文件名，null或空则读取最新文件</param>
    /// <param name="level">可选日志级别过滤（INFO/WARN/ERROR/DEBUG）</param>
    /// <returns>日志行列表</returns>
    public Object Logs(Int32 count, String file, String level)
    {
        if (!CheckAuth()) return new { code = 401, message = "Unauthorized" };

        if (count <= 0) count = 200;
        count = Math.Min(count, 1000);

        var lines = ReadLogLines(count, file, level);

        return new { code = 0, data = new { fileName = file ?? "latest", count = lines.Count, lines } };
    }

    /// <summary>获取日志文件列表</summary>
    /// <returns>日志文件列表</returns>
    public Object LogFiles()
    {
        if (!CheckAuth()) return new { code = 401, message = "Unauthorized" };

        var list = new List<Object>();

        try
        {
            var set = NewLife.Setting.Current;
            var logDir = set.LogPath.GetFullPath();
            if (Directory.Exists(logDir))
            {
                foreach (var filePath in Directory.GetFiles(logDir, "*.log").OrderByDescending(f => f))
                {
                    var fi = new FileInfo(filePath);
                    list.Add(new
                    {
                        name = fi.Name,
                        size = fi.Length,
                        sizeDisplay = fi.Length.ToGMK(),
                        lastModified = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        return new { code = 0, data = new { files = list } };
    }

    /// <summary>读取日志文件末尾行</summary>
    /// <param name="count">行数</param>
    /// <param name="fileName">指定日志文件名，null或空则读取最新文件</param>
    /// <param name="level">可选日志级别过滤（INFO/WARN/ERROR/DEBUG）</param>
    /// <returns>日志行列表</returns>
    private static List<String> ReadLogLines(Int32 count, String fileName = null, String level = null)
    {
        var list = new List<String>();

        try
        {
            var logDir = NewLife.Setting.Current.LogPath.GetFullPath();
            if (logDir == null) return list;

            String logFile;
            if (!fileName.IsNullOrEmpty())
            {
                var safeName = Path.GetFileName(fileName);
                logFile = Path.Combine(logDir, safeName);
                if (!File.Exists(logFile)) logFile = null;
            }
            else
            {
                logFile = Directory.GetFiles(logDir, "*.log")
                    .OrderByDescending(f => f)
                    .FirstOrDefault();
            }

            if (logFile == null) return list;

            // 使用 FileShare.ReadWrite 允许写入进程同时操作，避免文件共享冲突
            // 读取失败时重试一次，应对瞬态锁冲突
            var lines = ReadLinesWithRetry(logFile);
            if (lines == null || lines.Length == 0) return list;

            var start = Math.Max(0, lines.Length - count);
            for (var i = start; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!level.IsNullOrEmpty())
                {
                    if (!line.Contains(level, StringComparison.OrdinalIgnoreCase))
                        continue;
                }
                list.Add(line);
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        return list;
    }

    /// <summary>使用 FileShare.ReadWrite 读取文件所有行，写入冲突时自动重试</summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>所有行，失败返回空数组</returns>
    private static String[] ReadLinesWithRetry(String filePath)
    {
        for (var retry = 0; retry < 2; retry++)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                var lines = new List<String>();
                while (!sr.EndOfStream)
                {
                    lines.Add(sr.ReadLine());
                }
                return lines.ToArray();
            }
            catch (IOException) when (retry == 0)
            {
                Thread.Sleep(200);
            }
        }
        return [];
    }
    #endregion

    #region 健康
    /// <summary>获取健康指标</summary>
    /// <returns>健康指标数据，含内存、线程、句柄、GC等</returns>
    public Object Health()
    {
        if (!CheckAuth()) return new { code = 401, message = "Unauthorized" };

        var p = Process.GetCurrentProcess();
        var set = Setting.Current;

        return new
        {
            code = 0,
            data = new
            {
                memoryMB = p.WorkingSet64 / 1024 / 1024,
                memoryLimitMB = set.MaxMemory,
                threadCount = p.Threads.Count,
                threadLimit = set.MaxThread,
                handleCount = p.HandleCount,
                handleLimit = set.MaxHandle,
                totalProcessorTime = p.TotalProcessorTime.TotalSeconds.ToString("F1"),
                privilegedProcessorTime = p.PrivilegedProcessorTime.TotalSeconds.ToString("F1"),
                userProcessorTime = p.UserProcessorTime.TotalSeconds.ToString("F1"),
                gcTotalMemory = GC.GetTotalMemory(false) / 1024 / 1024,
                gcCollections = new
                {
                    gen0 = GC.CollectionCount(0),
                    gen1 = GC.CollectionCount(1),
                    gen2 = GC.CollectionCount(2)
                }
            }
        };
    }
    #endregion

    #region 看门狗
    /// <summary>获取看门狗状态</summary>
    /// <returns>看门狗监控的服务状态列表</returns>
    public Object WatchDog()
    {
        if (!CheckAuth()) return new { code = 401, message = "Unauthorized" };

        var set = Setting.Current;
        var dogServices = set.WatchDog?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !s.IsNullOrEmpty())
            .ToList() ?? [];

        var statuses = new List<Object>();
        foreach (var svc in dogServices)
        {
            var running = CheckServiceStatus(svc);
            statuses.Add(new { name = svc, running });
        }

        return new { code = 0, data = new { services = statuses } };
    }

    /// <summary>检查系统服务状态</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns>是否运行中，无法检查时返回null表示未知</returns>
    protected virtual Boolean? CheckServiceStatus(String serviceName)
    {
        try
        {
            var processes = Process.GetProcessesByName(serviceName);
            if (processes.Length > 0) return true;
        }
        catch { }
        return null;
    }
    #endregion

    #region 扩展
    /// <summary>获取扩展面板列表</summary>
    /// <returns>扩展面板配置列表</returns>
    public Object Extensions()
    {
        if (!CheckAuth()) return new { code = 401, message = "Unauthorized" };

        var panel = AgentWebPanel.Current;
        var extensions = panel?.GetExtensions() ?? [];
        return new { code = 0, data = new { panels = extensions } };
    }
    #endregion

    #region 辅助
    /// <summary>格式化网络速率为可读字符串</summary>
    /// <param name="bps">比特每秒</param>
    /// <returns>格式化后的速率字符串</returns>
    protected static String FormatSpeed(UInt64 bps)
    {
        return bps switch
        {
            < 1000UL => $"{bps} bps",
            < 1000_000UL => $"{bps / 1000.0:F1} Kbps",
            < 1000_000_000UL => $"{bps / 1000_000.0:F1} Mbps",
            _ => $"{bps / 1000_000_000.0:F2} Gbps"
        };
    }
    #endregion
}
#endif
