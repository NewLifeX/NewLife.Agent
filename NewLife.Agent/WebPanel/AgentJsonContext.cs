#if NET5_0_OR_GREATER
using System.Text.Json.Serialization;
using NewLife.Agent.WebPanel.Models;

namespace NewLife.Agent.WebPanel;

/// <summary>面板 API 响应的源生成上下文。标记类型由 NewLife.Core 源生成器注册零反射成员访问器</summary>
/// <remarks>
/// 该标记只提供序列化取值加速（JIT/AOT 双端免反射、免表达式编译），不保证属性可枚举性——
/// 可枚举性由 AotRoots 的 DynamicDependency 保证。新增响应 DTO 必须同步登记（有防漂移测试）。
/// </remarks>
[JsonSerializable(typeof(ApiResult))]
[JsonSerializable(typeof(ApiResult<LoginData>))]
[JsonSerializable(typeof(ApiResult<StatusData>))]
[JsonSerializable(typeof(ApiResult<ConfigMetadataData>))]
[JsonSerializable(typeof(ApiResult<LogContentData>))]
[JsonSerializable(typeof(ApiResult<LogFilesData>))]
[JsonSerializable(typeof(ApiResult<WatchDogData>))]
[JsonSerializable(typeof(ApiResult<HealthData>))]
[JsonSerializable(typeof(ApiResult<ExtensionsData>))]
[JsonSerializable(typeof(FreeMemoryResult))]
[JsonSerializable(typeof(LoginData))]
[JsonSerializable(typeof(StatusData))]
[JsonSerializable(typeof(FreeMemoryData))]
[JsonSerializable(typeof(ConfigItem))]
[JsonSerializable(typeof(ConfigMetadataData))]
[JsonSerializable(typeof(LogContentData))]
[JsonSerializable(typeof(LogFileItem))]
[JsonSerializable(typeof(LogFilesData))]
[JsonSerializable(typeof(WatchServiceItem))]
[JsonSerializable(typeof(WatchDogData))]
[JsonSerializable(typeof(HealthData))]
[JsonSerializable(typeof(GcCollectionsData))]
[JsonSerializable(typeof(ExtensionsData))]
[JsonSerializable(typeof(PanelExtension))]
internal partial class AgentJsonContext : JsonSerializerContext { }
#endif
