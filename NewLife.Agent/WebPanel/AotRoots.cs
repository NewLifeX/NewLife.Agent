#if !NET40
using System.Runtime.CompilerServices;

#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using NewLife.Agent.WebPanel.Models;

namespace NewLife.Agent.WebPanel;

/// <summary>NativeAOT 元数据保留根。保证面板响应 DTO 的公开属性在裁剪后仍可被反射枚举</summary>
/// <remarks>
/// 链接器不会因为源生成器（JsonMemberRegistrar）的静态 lambda 引用而保留"属性可枚举性"，
/// 必须以 DynamicDependency 显式声明；特性需挂在必然被执行的方法上（实测：静态构造器无效，被调用的 NoInlining 方法有效）。
/// 由 AgentWebPanel 构造函数调用 Ensure() 触发。新增响应 DTO 必须同步登记（UnitTest 有漂移防护测试）。
/// </remarks>
internal static class AotRoots
{
#if NET5_0_OR_GREATER
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ApiResult))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ApiResult<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(FreeMemoryResult))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LoginData))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(StatusData))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(FreeMemoryData))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ConfigItem))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ConfigMetadataData))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LogContentData))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LogFileItem))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LogFilesData))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(WatchServiceItem))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(WatchDogData))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(HealthData))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(GcCollectionsData))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ExtensionsData))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LanguageItem))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LanguagesData))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(PanelExtension))]
#endif
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Ensure() { }
}
#endif
