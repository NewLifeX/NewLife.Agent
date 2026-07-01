#if !NET40
using System.Diagnostics;
using System.Reflection;
using NewLife.Data;
using NewLife.Http;
using NewLife.Log;
using NewLife.Net;
using NewLife.Reflection;
using NewLife.Serialization;

namespace NewLife.Agent.WebPanel;

/// <summary>Agent Web管理面板 API 控制器</summary>
/// <remarks>
/// 提供 RESTful API 接口，包括服务状态查看、启停控制、配置管理、日志查看等功能。
/// 路由格式：/api/{MethodName}，由 HttpServer.MapController 自动映射。
/// </remarks>
public class ApiController
{
    #region 属性
    /// <summary>所属服务</summary>
    public ServiceBase Service => AgentWebPanel.Current?.Service!;

    /// <summary>鉴权处理器</summary>
    public AuthHandler Auth => AgentWebPanel.Current?.Auth!;
    #endregion

    #region 鉴权
    /// <summary>登录鉴权，签发Bearer Token</summary>
    /// <remarks>参数由 POST body 的 JSON 键值对整体反序列化传入，支持 {user, password}</remarks>
    /// <param name="credentials">登录凭据字典，含 user 和 password 键</param>
    /// <returns>Token信息，失败返回 code=401</returns>
    public Object Login(IDictionary<String, Object> credentials)
    {
        if (credentials == null)
            return new { code = 401, message = "Missing credentials" };

        var user = credentials.TryGetValue("user", out var u) ? u + "" : "";
        var password = credentials.TryGetValue("password", out var p) ? p + "" : "";

        var token = Auth.IssueToken(user, password);
        if (token == null)
            return new { code = 401, message = "Invalid credentials" };

        return new { code = 0, data = new { token } };
    }
    #endregion

    #region 状态
    /// <summary>获取服务状态</summary>
    /// <returns>服务状态信息，包含运行状态、进程信息、系统资源等</returns>
    public Object Status()
    {
        var p = Process.GetCurrentProcess();
        var uptime = DateTime.Now - p.StartTime;
        var mi = MachineInfo.Current ?? MachineInfo.GetCurrent();

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
                threadCount = p.Threads.Count,
                handleCount = p.HandleCount,
                startTime = p.StartTime.ToString("MM-dd HH:mm:ss"),
                hostMachine = Environment.MachineName,
                platform = mi.OSName ?? Environment.OSVersion.Platform.ToString(),
                osVersion = mi.OSVersion,
                cpuName = mi.Processor,
                cpuCount = Environment.ProcessorCount,
                cpuRate = mi.CpuRate > 0 ? mi.CpuRate.ToString("F1") : "",
                totalMemory = mi.Memory > 0 ? $"{mi.Memory / 1024 / 1024 / 1024} GB" : "",
                availableMemory = mi.AvailableMemory > 0 ? $"{mi.AvailableMemory / 1024 / 1024 / 1024} GB" : "",
                freeMemory = mi.FreeMemory > 0 ? $"{mi.FreeMemory / 1024 / 1024 / 1024} GB" : "",
                board = mi.Board,
                machineGuid = mi.Guid,
                uplinkSpeed = mi.UplinkSpeed > 0 ? FormatSpeed(mi.UplinkSpeed) : "",
                downlinkSpeed = mi.DownlinkSpeed > 0 ? FormatSpeed(mi.DownlinkSpeed) : "",
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
    /// 安全字段白名单：不更新密码字段。
    /// </remarks>
    /// <returns>更新结果</returns>
    public Object UpdateConfig(IDictionary<String, Object> updates)
    {
        if (updates == null || updates.Count == 0)
            return new { code = 400, message = "Missing config" };

        try
        {
            var set = Setting.Current;

            foreach (var kv in updates)
            {
                var prop = typeof(Setting).GetProperty(kv.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null || !prop.CanWrite) continue;

                // 安全字段白名单
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
    #endregion

    #region 日志
    /// <summary>获取日志内容</summary>
    /// <param name="count">读取行数，默认200，最大1000</param>
    /// <param name="file">指定日志文件名，null或空则读取最新文件</param>
    /// <param name="level">可选日志级别过滤（INFO/WARN/ERROR/DEBUG）</param>
    /// <returns>日志行列表</returns>
    public Object Logs(Int32 count, String file, String level)
    {
        if (count <= 0) count = 200;
        count = Math.Min(count, 1000);

        var lines = ReadLogLines(count, file, level);

        return new { code = 0, data = new { fileName = file ?? "latest", count = lines.Count, lines } };
    }

    /// <summary>获取日志文件列表</summary>
    /// <returns>日志文件列表</returns>
    public Object LogFiles()
    {
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

            var allLines = File.ReadAllLines(logFile);
            var start = Math.Max(0, allLines.Length - count);
            for (var i = start; i < allLines.Length; i++)
            {
                var line = allLines[i];
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
    #endregion

    #region 健康
    /// <summary>获取健康指标</summary>
    /// <returns>健康指标数据，含内存、线程、句柄、GC等</returns>
    public Object Health()
    {
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
