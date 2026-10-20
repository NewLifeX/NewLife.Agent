#if !NET40
using System.Collections.Generic;

namespace NewLife.Agent.WebPanel.Models;

/// <summary>面板 API 通用结果（code + message），对应 JSON 形状 <c>{code, message}</c></summary>
/// <remarks>键名由面板 JsonOptions（PropertyNaming.CamelCase）在面板启动时统一固定，与改造前匿名对象逐键一致；新增 DTO 的属性名经 CamelCase 转换后必须与既有前端契约一致，并同步登记到 AotRoots.Ensure() 与 AgentJsonContext（有防漂移测试）</remarks>
internal class ApiResult
{
    /// <summary>状态码，0 表示成功</summary>
    public Int32 Code { get; set; }

    /// <summary>消息文本</summary>
    public String Message { get; set; }
}

/// <summary>面板 API 数据结果（code + data），对应 JSON 形状 <c>{code, data}</c></summary>
/// <typeparam name="T">数据载荷类型</typeparam>
internal class ApiResult<T>
{
    /// <summary>状态码，0 表示成功</summary>
    public Int32 Code { get; set; }

    /// <summary>数据载荷</summary>
    public T Data { get; set; }
}

/// <summary>释放内存结果（code + message + data），对应 JSON 形状 <c>{code, message, data}</c></summary>
internal class FreeMemoryResult
{
    /// <summary>状态码，0 表示成功</summary>
    public Int32 Code { get; set; }

    /// <summary>消息文本</summary>
    public String Message { get; set; }

    /// <summary>数据载荷</summary>
    public FreeMemoryData Data { get; set; }
}

/// <summary>登录成功载荷</summary>
internal class LoginData
{
    /// <summary>访问令牌</summary>
    public String Token { get; set; }
}

/// <summary>服务状态载荷</summary>
internal class StatusData
{
    /// <summary>服务名</summary>
    public String ServiceName { get; set; }

    /// <summary>显示名</summary>
    public String DisplayName { get; set; }

    /// <summary>服务描述</summary>
    public String Description { get; set; }

    /// <summary>是否运行中</summary>
    public Boolean Running { get; set; }

    /// <summary>运行时长文本</summary>
    public String Uptime { get; set; }

    /// <summary>运行时长秒数</summary>
    public Int64 UptimeSeconds { get; set; }

    /// <summary>进程ID</summary>
    public Int32 ProcessId { get; set; }

    /// <summary>进程内存占用（MB）</summary>
    public Int64 MemoryMB { get; set; }

    /// <summary>物理内存总量（MB）</summary>
    public UInt64 MemoryTotalMB { get; set; }

    /// <summary>线程数</summary>
    public Int32 ThreadCount { get; set; }

    /// <summary>句柄数</summary>
    public Int32 HandleCount { get; set; }

    /// <summary>进程启动时间</summary>
    public String StartTime { get; set; }

    /// <summary>主机名</summary>
    public String HostMachine { get; set; }

    /// <summary>操作系统平台</summary>
    public String Platform { get; set; }

    /// <summary>操作系统版本</summary>
    public String OsVersion { get; set; }

    /// <summary>CPU 名称</summary>
    public String CpuName { get; set; }

    /// <summary>CPU 逻辑核数</summary>
    public Int32 CpuCount { get; set; }

    /// <summary>CPU 使用率文本（百分比）</summary>
    public String CpuRate { get; set; }

    /// <summary>CPU 使用率数值（百分比）</summary>
    public Double CpuRateValue { get; set; }

    /// <summary>物理内存总量文本</summary>
    public String TotalMemory { get; set; }

    /// <summary>可用内存文本</summary>
    public String AvailableMemory { get; set; }

    /// <summary>空闲内存文本</summary>
    public String FreeMemory { get; set; }

    /// <summary>主板信息</summary>
    public String Board { get; set; }

    /// <summary>机器唯一标识</summary>
    public String MachineGuid { get; set; }

    /// <summary>上行网速文本</summary>
    public String UplinkSpeed { get; set; }

    /// <summary>下行网速文本</summary>
    public String DownlinkSpeed { get; set; }

    /// <summary>TCP 已建立连接数</summary>
    public Int32 TcpConnections { get; set; }

    /// <summary>TCP TIME_WAIT 连接数</summary>
    public Int32 TcpTimeWait { get; set; }

    /// <summary>TCP CLOSE_WAIT 连接数</summary>
    public Int32 TcpCloseWait { get; set; }

    /// <summary>磁盘 IOPS</summary>
    public Int32 DiskIops { get; set; }

    /// <summary>主机开机时长文本</summary>
    public String HostUptime { get; set; }

    /// <summary>Web 面板端口</summary>
    public Int32 Port { get; set; }
}

/// <summary>释放内存载荷</summary>
internal class FreeMemoryData
{
    /// <summary>释放前内存（MB）</summary>
    public Int64 BeforeMB { get; set; }

    /// <summary>释放后内存（MB）</summary>
    public Int64 AfterMB { get; set; }

    /// <summary>释放量（MB）</summary>
    public Int64 FreedMB { get; set; }
}

/// <summary>配置项元数据与当前值</summary>
internal class ConfigItem
{
    /// <summary>属性名</summary>
    public String Name { get; set; }

    /// <summary>显示名</summary>
    public String DisplayName { get; set; }

    /// <summary>描述</summary>
    public String Description { get; set; }

    /// <summary>类型名</summary>
    public String Type { get; set; }

    /// <summary>当前值（基元/字符串）</summary>
    public Object Value { get; set; }
}

/// <summary>配置元数据载荷</summary>
internal class ConfigMetadataData
{
    /// <summary>配置项列表</summary>
    public List<ConfigItem> Items { get; set; }
}

/// <summary>日志内容载荷</summary>
internal class LogContentData
{
    /// <summary>文件名</summary>
    public String FileName { get; set; }

    /// <summary>行数</summary>
    public Int32 Count { get; set; }

    /// <summary>日志行</summary>
    public List<String> Lines { get; set; }
}

/// <summary>日志文件项</summary>
internal class LogFileItem
{
    /// <summary>文件名</summary>
    public String Name { get; set; }

    /// <summary>文件大小（字节）</summary>
    public Int64 Size { get; set; }

    /// <summary>大小显示文本</summary>
    public String SizeDisplay { get; set; }

    /// <summary>最后修改时间</summary>
    public String LastModified { get; set; }
}

/// <summary>日志文件列表载荷</summary>
internal class LogFilesData
{
    /// <summary>文件列表</summary>
    public List<LogFileItem> Files { get; set; }
}

/// <summary>看门狗监控的服务项</summary>
internal class WatchServiceItem
{
    /// <summary>服务名</summary>
    public String Name { get; set; }

    /// <summary>是否运行中，null 表示未知</summary>
    public Boolean? Running { get; set; }
}

/// <summary>看门狗状态载荷</summary>
internal class WatchDogData
{
    /// <summary>服务状态列表</summary>
    public List<WatchServiceItem> Services { get; set; }
}

/// <summary>健康指标载荷</summary>
internal class HealthData
{
    /// <summary>进程内存占用（MB）</summary>
    public Int64 MemoryMB { get; set; }

    /// <summary>内存上限（MB），0 表示不限</summary>
    public Int32 MemoryLimitMB { get; set; }

    /// <summary>线程数</summary>
    public Int32 ThreadCount { get; set; }

    /// <summary>线程数上限</summary>
    public Int32 ThreadLimit { get; set; }

    /// <summary>句柄数</summary>
    public Int32 HandleCount { get; set; }

    /// <summary>句柄数上限</summary>
    public Int32 HandleLimit { get; set; }

    /// <summary>总处理器时间（秒）</summary>
    public String TotalProcessorTime { get; set; }

    /// <summary>特权处理器时间（秒）</summary>
    public String PrivilegedProcessorTime { get; set; }

    /// <summary>用户处理器时间（秒）</summary>
    public String UserProcessorTime { get; set; }

    /// <summary>GC 总内存（MB）</summary>
    public Int64 GcTotalMemory { get; set; }

    /// <summary>GC 各代回收次数</summary>
    public GcCollectionsData GcCollections { get; set; }
}

/// <summary>GC 各代回收次数</summary>
internal class GcCollectionsData
{
    /// <summary>第 0 代回收次数</summary>
    public Int32 Gen0 { get; set; }

    /// <summary>第 1 代回收次数</summary>
    public Int32 Gen1 { get; set; }

    /// <summary>第 2 代回收次数</summary>
    public Int32 Gen2 { get; set; }
}

/// <summary>扩展面板列表载荷</summary>
internal class ExtensionsData
{
    /// <summary>扩展面板列表</summary>
    public List<PanelExtension> Panels { get; set; }
}

/// <summary>语言项</summary>
internal class LanguageItem
{
    /// <summary>语言代码，如 zh-CN</summary>
    public String Code { get; set; }

    /// <summary>语言显示名称</summary>
    public String Name { get; set; }
}

/// <summary>语言列表载荷</summary>
internal class LanguagesData
{
    /// <summary>当前固定语言。空表示跟随系统自动识别</summary>
    public String Current { get; set; }

    /// <summary>当前请求实际生效的语言</summary>
    public String Effective { get; set; }

    /// <summary>可选语言列表</summary>
    public List<LanguageItem> Items { get; set; }
}
#endif
