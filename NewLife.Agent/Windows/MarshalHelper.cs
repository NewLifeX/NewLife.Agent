using System;
using System.Runtime.InteropServices;

namespace NewLife.Agent.Windows;

/// <summary>封送辅助。net5+ 使用泛型重载（NativeAOT 下可静态折叠，免动态封送），旧目标回退非泛型</summary>
internal static class MarshalHelper
{
    /// <summary>获取结构体封送大小</summary>
    /// <typeparam name="T">结构体类型</typeparam>
    /// <returns>封送字节数</returns>
    public static Int32 SizeOf<T>() =>
#if NET5_0_OR_GREATER
        Marshal.SizeOf<T>();
#else
        Marshal.SizeOf(typeof(T));
#endif

    /// <summary>将非托管内存块封送为结构体</summary>
    /// <typeparam name="T">结构体类型</typeparam>
    /// <param name="ptr">非托管内存指针</param>
    /// <returns>封送后的结构体</returns>
    // net5+ 泛型重载要求 T 声明构造器元数据需求，调用方均为具体结构体类型，自动满足
    public static T PtrToStructure<
#if NET5_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicConstructors)]
#endif
        T>(IntPtr ptr) =>
#if NET5_0_OR_GREATER
        Marshal.PtrToStructure<T>(ptr);
#else
        (T)Marshal.PtrToStructure(ptr, typeof(T));
#endif
}
