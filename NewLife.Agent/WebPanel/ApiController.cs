#if !NET40
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using NewLife.Data;
using NewLife.Http;
using NewLife.Log;
using NewLife.Net;
using NewLife.Reflection;
using NewLife.Serialization;
using NewLife.Agent.WebPanel.Models;
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
            return new ApiResult { Code = 429, Message = L.T("Too many failed attempts. Try again later.") };

        var token = AgentWebPanel.Current?.IssueToken(user, password, ip);
        if (token == null)
        {
            LoginRateLimiter.RecordFailure(ip);
            return new ApiResult { Code = 401, Message = L.T("Invalid credentials") };
        }

        LoginRateLimiter.RecordSuccess(ip);

        return new ApiResult<LoginData> { Code = 0, Data = new LoginData { Token = token } };
    }

    /// <summary>注销登录，吊销当前Token</summary>
    /// <returns>操作结果</returns>
    public Object Logout()
    {
        var ctx = Context;
        var auth = ctx?.Request.Headers["Authorization"];
        if (auth.IsNullOrEmpty() || !auth.StartsWithIgnoreCase("Bearer "))
            return new ApiResult { Code = 0, Message = "ok" };

        var token = auth.Substring("Bearer ".Length).Trim();
        if (!token.IsNullOrEmpty())
            AgentWebPanel.Current?.RevokeToken(token);

        return new ApiResult { Code = 0, Message = "ok" };
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
    /// <remarks>
    /// 同机回环说明请求来自本机反向代理（nginx 等），此时以 X-Forwarded-For 首个地址作为真实客户端。
    /// 否则反代后所有用户共用 127.0.0.1，任意人连错几次就能把全员登录锁死。
    /// </remarks>
    /// <returns>IP字符串，无法获取时返回 unknown</returns>
    protected String GetClientIp()
    {
        var ctx = Context;
        var socket = ctx?.Socket;
        if (socket?.Remote == null || socket.Remote.Address == null) return "unknown";

        var address = socket.Remote.Address;
        if (IPAddress.IsLoopback(address))
        {
            var forwarded = ctx.Request.Headers["X-Forwarded-For"];
            if (!forwarded.IsNullOrEmpty())
            {
                var first = forwarded.Split(',')[0].Trim();
                if (IPAddress.TryParse(first, out var real)) return real.ToString();
            }
        }

        return address + "";
    }
    #endregion

    #region 状态
    /// <summary>获取服务状态</summary>
    /// <returns>服务状态信息，包含运行状态、进程信息、系统资源等</returns>
    public Object Status()
    {
        if (!CheckAuth()) return new ApiResult { Code = 401, Message = L.T("Unauthorized") };

        var p = Process.GetCurrentProcess();
        var uptime = DateTime.Now - p.StartTime;
        var mi = MachineInfo.Current ?? MachineInfo.GetCurrent();

        // 系统级采样（CPU/网络/TCP/磁盘）按 2 秒缓存：前端每 10 秒轮询，多个标签页同时打开时不必每个请求都采一次
        Int32 tcpConnections, tcpTimeWait, tcpCloseWait, diskIops;
        lock (_statusLock)
        {
            var now = Runtime.TickCount64;
            if (now - _statusSampleTick >= StatusCacheMilliseconds)
            {
                _statusSampleTick = now;
                SampleSystem(mi);
            }

            tcpConnections = _tcpConnections;
            tcpTimeWait = _tcpTimeWait;
            tcpCloseWait = _tcpCloseWait;
            diskIops = _diskIops;
        }

        return new ApiResult<StatusData>
        {
            Code = 0,
            Data = new StatusData
            {
                ServiceName = Service.ServiceName,
                DisplayName = Service.DisplayTitle,
                Description = Service.DisplayDescription,
                Running = Service.Running,
                Uptime = uptime.ToString(@"d\.hh\:mm\:ss"),
                UptimeSeconds = (Int64)uptime.TotalSeconds,
                ProcessId = p.Id,
                MemoryMB = p.WorkingSet64 / 1024 / 1024,
                // 物理内存总量
                MemoryTotalMB = mi.Memory > 0 ? mi.Memory / 1024 / 1024 : 0,
                ThreadCount = p.Threads.Count,
                HandleCount = p.HandleCount,
                StartTime = p.StartTime.ToString("MM-dd HH:mm:ss"),
                HostMachine = Environment.MachineName,
                Platform = mi.OSName ?? Environment.OSVersion.Platform.ToString(),
                OsVersion = mi.OSVersion,
                CpuName = mi.Processor,
                CpuCount = Environment.ProcessorCount,
                // CPU 使用率百分比（0~100），保留一位小数
                CpuRate = mi.CpuRate > 0 ? mi.CpuRate.ToString("F1") : "",
                CpuRateValue = mi.CpuRate > 0 ? Math.Round(mi.CpuRate, 1) : 0d,
                TotalMemory = mi.Memory > 0 ? $"{mi.Memory / 1024 / 1024 / 1024} GB" : "",
                AvailableMemory = mi.AvailableMemory > 0 ? $"{mi.AvailableMemory / 1024 / 1024 / 1024} GB" : "",
                FreeMemory = mi.FreeMemory > 0 ? $"{mi.FreeMemory / 1024 / 1024 / 1024} GB" : "",
                Board = mi.Board,
                MachineGuid = mi.Guid,
                UplinkSpeed = mi.UplinkSpeed > 0 ? FormatSpeed(mi.UplinkSpeed) : "",
                DownlinkSpeed = mi.DownlinkSpeed > 0 ? FormatSpeed(mi.DownlinkSpeed) : "",
                // TCP 连接数
                TcpConnections = tcpConnections,
                TcpTimeWait = tcpTimeWait,
                TcpCloseWait = tcpCloseWait,
                // 磁盘 IOPS
                DiskIops = diskIops,
                HostUptime = TimeSpan.FromMilliseconds(Runtime.TickCount64).ToString(@"d\.hh\:mm\:ss"),
                Port = AgentWebPanel.Current?.Server.Port ?? 0
            }
        };
    }

    /// <summary>系统采样缓存时长（毫秒）</summary>
    private const Int32 StatusCacheMilliseconds = 2_000;

    private static readonly Object _statusLock = new();
    private static Int64 _statusSampleTick;
    private static Int32 _tcpConnections;
    private static Int32 _tcpTimeWait;
    private static Int32 _tcpCloseWait;
    private static Int32 _diskIops;

    /// <summary>采集系统级指标，结果写入缓存字段</summary>
    /// <param name="mi">机器信息，用于刷新 CPU 使用率与网络速度</param>
    private static void SampleSystem(MachineInfo mi)
    {
        mi.Refresh();
        mi.RefreshSpeed();

        try
        {
            var connections = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
            _tcpConnections = connections.Count(e => e.State == TcpState.Established);
            _tcpTimeWait = connections.Count(e => e.State == TcpState.TimeWait);
            _tcpCloseWait = connections.Count(e => e.State == TcpState.CloseWait);
        }
        catch
        {
            _tcpConnections = 0;
            _tcpTimeWait = 0;
            _tcpCloseWait = 0;
        }

        _diskIops = DiskMonitor.GetIOPS();
    }
    #endregion

    #region 控制
    /// <summary>服务控制（启停重启）</summary>
    /// <param name="action">操作类型：start/stop/restart</param>
    /// <returns>操作结果</returns>
    public Object Control(String action)
    {
        if (!CheckAuth()) return new ApiResult { Code = 401, Message = L.T("Unauthorized") };

        if (action.IsNullOrEmpty())
            return new ApiResult { Code = 400, Message = L.T("Missing action") };

        switch (action.ToLower())
        {
            case "stop":
                XTrace.Log.WriteLogPrefixed(LogLevel.Info, "[WebPanel]", "The web panel triggered a service stop.");
                Service.Running = false;
                return new ApiResult { Code = 0, Message = L.T("The service is stopping; the web panel remains available.") };

            case "start":
                XTrace.Log.WriteLogPrefixed(LogLevel.Info, "[WebPanel]", "The web panel triggered a service start.");
                Service.Host.Restart(Service.ServiceName);
                return new ApiResult { Code = 0, Message = L.T("The service is restarting.") };

            case "restart":
                XTrace.Log.WriteLogPrefixed(LogLevel.Info, "[WebPanel]", "The web panel triggered a service restart.");
                Service.Host.Restart(Service.ServiceName);
                return new ApiResult { Code = 0, Message = L.T("The service is restarting.") };

            default:
                return new ApiResult { Code = 400, Message = L.T("Unknown action: {0}", action) };
        }
    }

    /// <summary>释放内存。GC回收后再释放工作集</summary>
    /// <returns>释放前后的内存对比</returns>
    public Object FreeMemory()
    {
        if (!CheckAuth()) return new ApiResult { Code = 401, Message = L.T("Unauthorized") };

        var p = Process.GetCurrentProcess();
        var beforeMB = p.WorkingSet64 / 1024 / 1024;

        XTrace.Log.WriteLogPrefixed(LogLevel.Info, "[WebPanel]", "The web panel triggered memory release; WorkingSet before release={0} MB.", beforeMB);

        var success = Runtime.FreeMemory();

        p.Refresh();
        var afterMB = p.WorkingSet64 / 1024 / 1024;
        var freedMB = beforeMB - afterMB;

        XTrace.Log.WriteLogPrefixed(LogLevel.Info, "[WebPanel]", "Memory release completed; WorkingSet after release={0} MB, freed {1} MB.", afterMB, freedMB);

        return new FreeMemoryResult
        {
            Code = success ? 0 : 500,
            Message = success ? L.T("Memory release completed: {0} MB -> {1} MB, freed {2} MB.", beforeMB, afterMB, freedMB) : L.T("Memory release failed."),
            Data = new FreeMemoryData
            {
                BeforeMB = beforeMB,
                AfterMB = afterMB,
                FreedMB = freedMB
            }
        };
    }
    #endregion

    #region 配置
    /// <summary>配置属性元数据缓存项</summary>
    private class ConfigPropertyInfo
    {
        public String Name;
        public String Description;
        public String TypeName;
        public PropertyInfo Property;
    }

    // 配置元数据（属性名/显示名/描述/类型）不随配置值变化，静态缓存避免每次请求全量反射
    private static readonly ConfigPropertyInfo[] _configProperties = BuildConfigProperties();

    /// <summary>构建配置属性元数据缓存。排除密码字段（走 ChangePassword 接口）</summary>
    /// <returns>配置属性元数据数组</returns>
    private static ConfigPropertyInfo[] BuildConfigProperties()
    {
        var list = new List<ConfigPropertyInfo>();
        foreach (var prop in typeof(Setting).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || !prop.CanWrite) continue;
            if (IsSensitive(prop.Name)) continue;

            var description = prop.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description ?? "";

            var typeName = prop.PropertyType.Name switch
            {
                "String" => "String",
                "Int32" => "Int32",
                "Boolean" => "Boolean",
                _ => prop.PropertyType.Name
            };

            list.Add(new ConfigPropertyInfo
            {
                Name = prop.Name,
                Description = description,
                TypeName = typeName,
                Property = prop
            });
        }
        return list.ToArray();
    }

    /// <summary>判断配置项是否属于敏感字段（密码、密钥），敏感字段只写不读</summary>
    /// <param name="name">配置项名称</param>
    /// <returns>是否敏感</returns>
    internal static Boolean IsSensitive(String name)
    {
        if (name.IsNullOrEmpty()) return false;

        return name.EndsWith("Password", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("Secret", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>允许从面板修改的配置字段白名单</summary>
    /// <remarks>
    /// 用白名单而不是“密码/密钥”黑名单：AfterStart 会在服务启动时执行命令，ServiceName 与 DisplayName 会拼接进服务控制命令与
    /// 以 root 身份执行的启动脚本，这些字段一旦可改，等于给面板用户一个持久化的命令执行入口。
    /// 新增配置项默认不可从面板修改，需要时显式加到这里。
    /// </remarks>
    internal static readonly HashSet<String> WritableConfig = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(Setting.WatchInterval),
        nameof(Setting.FreeMemoryInterval),
        nameof(Setting.MaxMemory),
        nameof(Setting.MaxThread),
        nameof(Setting.MaxHandle),
        nameof(Setting.AutoRestart),
        nameof(Setting.RestartTimeRange),
        nameof(Setting.WatchDog),
        nameof(Setting.EnableWebPanel),
        nameof(Setting.WebPort),
        nameof(Setting.WebBind),
        nameof(Setting.UseAutorun),
    };

    /// <summary>判断配置字段是否允许从面板修改</summary>
    /// <param name="name">配置项名称</param>
    /// <returns>是否允许修改</returns>
    internal static Boolean IsWritableConfig(String name) => !name.IsNullOrEmpty() && WritableConfig.Contains(name);

    /// <summary>获取配置元数据（DisplayName + Description + Type + Value），供前端三列布局渲染</summary>
    /// <returns>配置项列表，含显示名、描述、类型和当前值</returns>
    public Object ConfigMetadata()
    {
        if (!CheckAuth()) return new ApiResult { Code = 401, Message = L.T("Unauthorized") };

        var set = Setting.Current;
        var items = new List<ConfigItem>();

        // 元数据来自静态缓存，仅值需实时读取；描述按当前语言翻译，显示名取译文首句
        foreach (var prop in _configProperties)
        {
            var description = L.T(prop.Description);
            items.Add(new ConfigItem
            {
                Name = prop.Name,
                DisplayName = GetDisplayName(description),
                Description = description,
                Type = prop.TypeName,
                Value = prop.Property.GetValue(set, null)
            });
        }

        return new ApiResult<ConfigMetadataData> { Code = 0, Data = new ConfigMetadataData { Items = items } };
    }

    /// <summary>从配置描述中取首句作为显示名</summary>
    /// <param name="description">已翻译的配置描述</param>
    /// <returns>显示名</returns>
    internal static String GetDisplayName(String description)
    {
        if (description.IsNullOrEmpty()) return "";

        var index = description.IndexOfAny(['.', '。', '।']);
        if (index > 0) return description[..index];

        // 泰语等无句末标点的长描述，取首个空格前的短语作为显示名；短标签整体返回
        if (description.Length <= 40) return description;

        var space = description.IndexOf(' ');
        return space > 0 ? description[..space] : description;
    }

    /// <summary>更新配置</summary>
    /// <remarks>
    /// 接收 JSON 对象，key 为属性名，value 为新值。
    /// 安全字段白名单：不更新密码字段（请使用 ChangePassword 接口）。
    /// 修改监听地址或端口时热重启面板（无需重启服务），并把新地址回传给前端跳转。
    /// </remarks>
    /// <returns>更新结果</returns>
    public Object UpdateConfig(IDictionary<String, Object> updates)
    {
        if (!CheckAuth()) return new ApiResult { Code = 401, Message = L.T("Unauthorized") };

        if (updates == null || updates.Count == 0)
            return new ApiResult { Code = 400, Message = L.T("Missing config") };

        var set = Setting.Current;
        var oldBind = set.WebBind;
        var oldPort = set.WebPort;

        // 两阶段应用：先全部校验与转换，再统一写入。避免中途失败留下“改了一半”的内存配置
        var values = new Dictionary<PropertyInfo, Object>();
        var backup = new Dictionary<PropertyInfo, Object>();
        try
        {
            foreach (var kv in updates)
            {
                var prop = typeof(Setting).GetProperty(kv.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null || !prop.CanWrite) continue;

                // 白名单：只有明确允许的字段才能从面板改写，其余（口令、启动命令、服务名等）一律拒绝
                if (!IsWritableConfig(kv.Key))
                    return new ApiResult { Code = 403, Message = L.T("The setting {0} cannot be modified from the web panel.", kv.Key) };

                var value = kv.Value;
                if (value != null && value.GetType() != prop.PropertyType)
                    value = Reflect.ChangeType(value, prop.PropertyType);

                values[prop] = value;
                backup[prop] = prop.GetValue(set, null);
            }

            foreach (var kv in values)
                kv.Key.SetValue(set, kv.Value, null);
        }
        catch (Exception ex)
        {
            // 回滚已写入的字段，内存配置与落盘配置必须保持一致；错误信息只记日志，不回传内部细节
            foreach (var kv in backup)
            {
                try { kv.Key.SetValue(set, kv.Value, null); } catch { }
            }

            RestoreBinding(set, oldBind, oldPort);

            XTrace.WriteException(ex);

            return new ApiResult { Code = 400, Message = L.T("Invalid value for one or more settings.") };
        }

        var bindChanged = !String.Equals(oldBind?.Trim(), set.WebBind?.Trim(), StringComparison.OrdinalIgnoreCase);
        var portChanged = oldPort != set.WebPort;
        var panel = AgentWebPanel.Current;

        // 未涉及监听地址/端口，直接落盘
        if ((!bindChanged && !portChanged) || panel == null)
        {
            set.Save();

            return new ApiResult { Code = 0, Message = L.T("Configuration updated; some settings take effect after the service restarts.") };
        }

        // 端口必须是明确值，0 会让面板落到随机端口而无法回传
        if (set.WebPort < 1 || set.WebPort > 65535)
        {
            RestoreBinding(set, oldBind, oldPort);

            return new ApiResult { Code = 400, Message = L.T("Invalid port. Use a value between 1 and 65535.") };
        }

        // 默认口令下不允许把面板暴露到网络，必须先改口令
        if (!AgentWebPanel.IsLoopbackBind(set.WebBind) && panel.IsDefaultPassword)
        {
            RestoreBinding(set, oldBind, oldPort);

            return new ApiResult { Code = 400, Message = L.T("Change the web panel password before exposing the panel to the network.") };
        }

        if (!panel.RequestRebind(bindChanged ? set.WebBind : null, portChanged ? set.WebPort : null))
        {
            RestoreBinding(set, oldBind, oldPort);

            return new ApiResult { Code = 429, Message = L.T("The web panel is restarting; try again later.") };
        }

        var address = AgentWebPanel.ParseBindAddress(set.WebBind);

        return new ConfigUpdateResult
        {
            Code = 0,
            Message = L.T("The web panel is moving to the new address and will reopen shortly. Sign in again if prompted."),
            Data = new ConfigUpdateData
            {
                Port = set.WebPort,
                ListenAll = address == null,
                Address = address?.ToString(),
            }
        };
    }

    /// <summary>回滚监听地址与端口的内存改动</summary>
    /// <param name="set">配置实例</param>
    /// <param name="bind">原绑定地址</param>
    /// <param name="port">原端口</param>
    private static void RestoreBinding(Setting set, String bind, Int32 port)
    {
        set.WebBind = bind;
        set.WebPort = port;
    }

    /// <summary>修改密码。校验旧密码后更新为新密码，立即生效无需重启</summary>
    /// <param name="oldPassword">旧密码</param>
    /// <param name="newPassword">新密码</param>
    /// <returns>操作结果</returns>
    public Object ChangePassword(String oldPassword, String newPassword)
    {
        if (!CheckAuth()) return new ApiResult { Code = 401, Message = L.T("Unauthorized") };

        if (oldPassword.IsNullOrEmpty()) return new ApiResult { Code = 400, Message = L.T("Missing oldPassword") };
        if (newPassword.IsNullOrEmpty()) return new ApiResult { Code = 400, Message = L.T("Missing newPassword") };

        var panel = AgentWebPanel.Current;
        if (panel == null) return new ApiResult { Code = 500, Message = L.T("Panel not available") };

        if (newPassword.Length < 4)
            return new ApiResult { Code = 400, Message = L.T("The new password must be at least 4 characters.") };

        // 旧口令校验同样限速：令牌被窃后不能靠本地无限次猜测旧口令完成劫持
        var ip = GetClientIp();
        if (LoginRateLimiter.IsBlocked(ip))
            return new ApiResult { Code = 429, Message = L.T("Too many failed attempts. Try again later.") };

        // 验证旧密码
        if (!panel.Authenticate(panel.UserName, oldPassword))
        {
            LoginRateLimiter.RecordFailure(ip);

            return new ApiResult { Code = 403, Message = L.T("Old password is incorrect") };
        }

        LoginRateLimiter.RecordSuccess(ip);

        // 加盐哈希后写入配置并立即生效
        panel.SetPassword(newPassword);

        XTrace.Log.WriteLogPrefixed(LogLevel.Info, "[WebPanel]", "The web panel password has been changed.");

        return new ApiResult { Code = 0, Message = L.T("Password changed; use the new password on your next login.") };
    }
    #endregion

    #region 语言
    /// <summary>获取可用界面语言</summary>
    /// <returns>语言代码、显示名称、配置选中语言与实际生效语言；语言列表不敏感，未登录也可读取</returns>
    /// <remarks>只列出真正有词表的语言。裁剪过语言词表的小体积发布（如 NativeAOT）会因此只返回少数语言</remarks>
    public Object Languages()
    {
        var items = new List<LanguageItem>();
        foreach (var item in Localization.GetLanguageOptions())
        {
            items.Add(new LanguageItem { Code = item.Key, Name = item.Value });
        }

        var selected = PanelLanguage.Selected;

        return new ApiResult<LanguagesData>
        {
            Code = 0,
            Data = new LanguagesData
            {
                Current = Localization.ForcedLanguage ?? "",
                Selected = selected,
                Forced = !selected.IsNullOrEmpty(),
                Effective = PanelLanguage.Effective,
                Items = items
            }
        };
    }

    /// <summary>设置界面与日志语言，写入 Core 全局配置并立即生效</summary>
    /// <param name="language">语言代码，留空表示跟随系统自动识别</param>
    /// <returns>操作结果</returns>
    public Object SetLanguage(String language)
    {
        if (!CheckAuth()) return new ApiResult { Code = 401, Message = L.T("Unauthorized") };

        var name = Localization.NormalizeCulture(language);
        if (name == null && !language.IsNullOrWhiteSpace())
            return new ApiResult { Code = 400, Message = L.T("Unsupported language: {0}", language) };

        var set = NewLife.Setting.Current;
        set.Language = name ?? "";
        set.Save();

        return new ApiResult { Code = 0, Message = L.T("Language updated.") };
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
        if (!CheckAuth()) return new ApiResult { Code = 401, Message = L.T("Unauthorized") };

        if (count <= 0) count = 200;
        count = Math.Min(count, 1000);

        var lines = ReadLogLines(count, file, level);

        return new ApiResult<LogContentData> { Code = 0, Data = new LogContentData { FileName = file ?? "latest", Count = lines.Count, Lines = lines } };
    }

    /// <summary>获取日志文件列表</summary>
    /// <returns>日志文件列表</returns>
    public Object LogFiles()
    {
        if (!CheckAuth()) return new ApiResult { Code = 401, Message = L.T("Unauthorized") };

        var list = new List<LogFileItem>();

        try
        {
            var set = NewLife.Setting.Current;
            var logDir = set.LogPath.GetFullPath();
            if (Directory.Exists(logDir))
            {
                foreach (var filePath in Directory.GetFiles(logDir, "*.log").OrderByDescending(f => f))
                {
                    var fi = new FileInfo(filePath);
                    list.Add(new LogFileItem
                    {
                        Name = fi.Name,
                        Size = fi.Length,
                        SizeDisplay = fi.Length.ToGMK(),
                        LastModified = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        return new ApiResult<LogFilesData> { Code = 0, Data = new LogFilesData { Files = list } };
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

            // 只读取文件尾部：日志会长期增长，整文件读入会造成大对象分配与长时间阻塞
            var lines = ReadTailLines(logFile, Math.Max(count, 1));
            if (lines == null || lines.Length == 0) return list;

            foreach (var line in lines)
            {
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

    /// <summary>单次日志读取的最大字节数</summary>
    private const Int32 MaxLogReadBytes = 1024 * 1024;

    /// <summary>从文件尾部读取日志行，读取量不超过 <see cref="MaxLogReadBytes"/></summary>
    /// <param name="filePath">日志文件路径</param>
    /// <param name="maxLines">最多返回的行数</param>
    /// <returns>尾部若干行；失败返回空数组</returns>
    private static String[] ReadTailLines(String filePath, Int32 maxLines)
    {
        for (var retry = 0; retry < 2; retry++)
        {
            try
            {
                // 使用 FileShare.ReadWrite 允许写入进程同时操作，避免文件共享冲突
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var length = fs.Length;
                var size = (Int32)Math.Min(length, MaxLogReadBytes);
                var start = length - size;

                // 从中间开始时，块首可能是半行，读取后丢弃
                if (start > 0) fs.Seek(start, SeekOrigin.Begin);

                var buffer = new Byte[size];
                var read = fs.Read(buffer, 0, size);
                var text = Encoding.UTF8.GetString(buffer, 0, read);

                var all = text.Split('\n');
                var list = new List<String>(all.Length);
                for (var i = start > 0 ? 1 : 0; i < all.Length; i++)
                {
                    var line = all[i].TrimEnd('\r');
                    // 文件以换行结尾时最后会多出一个空串
                    if (i == all.Length - 1 && line.Length == 0) continue;

                    list.Add(line);
                }

                if (list.Count > maxLines) list.RemoveRange(0, list.Count - maxLines);

                return [.. list];
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
        if (!CheckAuth()) return new ApiResult { Code = 401, Message = L.T("Unauthorized") };

        var p = Process.GetCurrentProcess();
        var set = Setting.Current;

        return new ApiResult<HealthData>
        {
            Code = 0,
            Data = new HealthData
            {
                MemoryMB = p.WorkingSet64 / 1024 / 1024,
                MemoryLimitMB = set.MaxMemory,
                ThreadCount = p.Threads.Count,
                ThreadLimit = set.MaxThread,
                HandleCount = p.HandleCount,
                HandleLimit = set.MaxHandle,
                TotalProcessorTime = p.TotalProcessorTime.TotalSeconds.ToString("F1"),
                PrivilegedProcessorTime = p.PrivilegedProcessorTime.TotalSeconds.ToString("F1"),
                UserProcessorTime = p.UserProcessorTime.TotalSeconds.ToString("F1"),
                GcTotalMemory = GC.GetTotalMemory(false) / 1024 / 1024,
                GcCollections = new GcCollectionsData
                {
                    Gen0 = GC.CollectionCount(0),
                    Gen1 = GC.CollectionCount(1),
                    Gen2 = GC.CollectionCount(2)
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
        if (!CheckAuth()) return new ApiResult { Code = 401, Message = L.T("Unauthorized") };

        var set = Setting.Current;
        var dogServices = set.WatchDog?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !s.IsNullOrEmpty())
            .ToList() ?? [];

        var statuses = new List<WatchServiceItem>();
        foreach (var svc in dogServices)
        {
            var running = CheckServiceStatus(svc);
            statuses.Add(new WatchServiceItem { Name = svc, Running = running });
        }

        return new ApiResult<WatchDogData> { Code = 0, Data = new WatchDogData { Services = statuses } };
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
        if (!CheckAuth()) return new ApiResult { Code = 401, Message = L.T("Unauthorized") };

        var panel = AgentWebPanel.Current;
        var extensions = panel?.GetExtensions() ?? [];
        return new ApiResult<ExtensionsData> { Code = 0, Data = new ExtensionsData { Panels = extensions } };
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
