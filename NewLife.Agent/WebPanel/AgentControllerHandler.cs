#if !NET40
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using NewLife.Http;
using NewLife.Reflection;
using NewLife.Remoting;
using NewLife.Serialization;

namespace NewLife.Agent.WebPanel;

/// <summary>Agent控制器处理器，在 ControllerHandler 基础上增加单参数 fallback 绑定</summary>
/// <remarks>
/// 当方法只有一个参数且无法按名称匹配时（如 POST body 的 JSON 键值对），
/// 将整个参数字典反序列化为该参数类型，类似 DelegateHandler.OnInvoke 的 fallback 逻辑。
/// </remarks>
internal class AgentControllerHandler : IHttpHandler
{
    #region 属性
    /// <summary>控制器类型</summary>
    public Type? ControllerType { get; set; }

    private readonly ConcurrentDictionary<String, MethodInfo?> _methods = new();
    #endregion

    /// <summary>处理请求</summary>
    /// <param name="context">Http上下文</param>
    public void ProcessRequest(IHttpContext context)
    {
        var type = ControllerType;
        if (type == null) return;

        var ss = context.Path.Split('/');
        var methodName = ss.Length >= 3 ? ss[2] : null;

        var serviceProvider = context.ServiceProvider;
        var controller = serviceProvider?.GetService(type) ?? type.CreateInstance();

        // 查找方法，增加缓存
        MethodInfo? method = null;
        if (methodName != null && !_methods.TryGetValue(methodName, out method))
        {
            method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.IgnoreCase);
            _methods[methodName] = method;
        }
        if (method == null) throw new ApiException(ApiCode.NotFound, $"Cannot find operation [{methodName}] within controller [{type.FullName}]");

        // 参数绑定（含单参数 fallback）
        var parameters = context.Parameters;
        var pis = method.GetParameters();
        var args = new Object?[pis.Length];
        for (var i = 0; i < pis.Length; i++)
        {
            if (parameters.TryGetValue(pis[i].Name + "", out var v))
                args[i] = v.ChangeType(pis[i].ParameterType);
            else if (pis[i].HasDefaultValue)
                args[i] = pis[i].DefaultValue;
        }

        // 单参数 fallback：整个参数字典反序列化为参数类型（支持 IDictionary 等）
        if (args.Length == 1 && args[0] == null)
            args[0] = JsonHelper.Default.Convert(parameters, pis[0].ParameterType);

        var result = controller.Invoke(method, args);
        if (result is Task task)
        {
            task.GetAwaiter().GetResult();
            var taskType = task.GetType();
            if (taskType.IsGenericType)
            {
                var resultProperty = taskType.GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
                result = resultProperty?.GetValue(task);
            }
            else
            {
                result = null;
            }
        }
        if (result != null)
            context.Response.SetResult(result);
    }
}
#endif
