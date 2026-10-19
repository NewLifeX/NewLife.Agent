using System.Reflection;
using NewLife.Agent.CommandHandler;
using NewLife.Agent.Models;

namespace NewLife.Agent.Command;

/// <summary>
/// 命令工厂
/// </summary>
public class CommandFactory
{
    private readonly List<BaseCommandHandler> _commandHandlerList;
    private readonly Dictionary<String, BaseCommandHandler> _commandHandlerDict = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>内建命令处理器工厂表</summary>
    /// <remarks>内建处理器经静态工厂实例化，避免 Assembly.GetTypes + Activator 在 NativeAOT 裁剪后命令静默丢失；新增内建处理器必须同步登记，UnitTest 有漂移防护测试</remarks>
    private static readonly Func<ServiceBase, BaseCommandHandler>[] _builtinFactories =
    [
        s => new Install(s),
        s => new InstallAndStart(s),
        s => new Reinstall(s),
        s => new Remove(s),
        s => new Restart(s),
        s => new RunService(s),
        s => new RunSimulation(s),
        s => new ShowStatus(s),
        s => new Start(s),
        s => new Stop(s),
        s => new Uninstall(s),
        s => new WatchDog(s),
    ];

    /// <summary>
    /// 命令工厂
    /// </summary>
    /// <param name="service">服务对象</param>
    /// <param name="customCommandHandlerAssembly">自定义命令处理程序所在程序集</param>
    public CommandFactory(ServiceBase service, params Assembly[] customCommandHandlerAssembly)
    {
        var commandHandlers = new List<BaseCommandHandler>();

        // 内建命令：静态工厂实例化
        foreach (var factory in _builtinFactories)
        {
            AddCommandHandler(commandHandlers, factory(service));
        }

        var assemblies = new Dictionary<String, Assembly>();
        var baseAssembly = Assembly.GetExecutingAssembly();
        var serviceAssembly = Assembly.GetAssembly(service.GetType());
        assemblies[baseAssembly.FullName] = baseAssembly;
        assemblies[serviceAssembly.FullName] = serviceAssembly;
        if (customCommandHandlerAssembly?.Length > 0)
        {
            foreach (var assembly in customCommandHandlerAssembly)
            {
                if (!assemblies.ContainsKey(assembly.FullName))
                {
                    assemblies[assembly.FullName] = assembly;
                }
            }
        }

        // 使用反射发现并实例化扩展程序集中的自定义命令处理器；基础程序集的内建处理器已由静态工厂登记，不重复扫描
        foreach (var handler in FindCustomCommandHandlers(assemblies, baseAssembly, service))
        {
            AddCommandHandler(commandHandlers, handler);
        }

        _commandHandlerList = commandHandlers.OrderBy(n => n.Cmd).ToList();
    }

    /// <summary>反射发现并实例化扩展程序集中的自定义命令处理器</summary>
    /// <remarks>
    /// 内建命令处理器已由静态工厂登记，NativeAOT 下不受裁剪影响；
    /// 自定义处理器依赖类型元数据保留，NativeAOT 场景需调用方自行保证（显式保留或改用静态注册）。
    /// </remarks>
    /// <param name="assemblies">候选程序集（服务程序集与自定义程序集）</param>
    /// <param name="baseAssembly">基础程序集（跳过，内建处理器已静态登记）</param>
    /// <param name="service">服务对象</param>
    /// <returns>自定义命令处理器列表</returns>
#if NET5_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2062", Justification = "实例化的类型来自调用方保留的扩展程序集")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "实例化的类型来自调用方保留的扩展程序集")]
#endif
    private static List<BaseCommandHandler> FindCustomCommandHandlers(Dictionary<String, Assembly> assemblies, Assembly baseAssembly, ServiceBase service)
    {
        var list = new List<BaseCommandHandler>();
        foreach (var assembly in assemblies.Values)
        {
            if (assembly == baseAssembly) continue;
            // 动态程序集（如单元测试代理程序集）：类型无法枚举，也不可能承载自定义处理器
            if (assembly.IsDynamic) continue;

            foreach (var type in GetLoadableTypes(assembly))
            {
                if (!typeof(BaseCommandHandler).IsAssignableFrom(type) || type.IsInterface || type.IsAbstract) continue;
                list.Add((BaseCommandHandler)Activator.CreateInstance(type, service));
            }
        }

        return list;
    }

    /// <summary>获取程序集内可加载类型；个别类型加载失败时跳过，不影响其余类型</summary>
    /// <param name="assembly">目标程序集</param>
    /// <returns>可加载类型集合</returns>
#if NET5_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "仅扫描扩展程序集；NativeAOT 下自定义处理器的元数据保留由调用方负责，内建命令已静态登记不受影响")]
#endif
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null).Select(t => t!);
        }
    }

    /// <summary>登记命令处理器，并处理相同命令的冲突</summary>
    /// <param name="commandHandlers">处理器列表</param>
    /// <param name="handler">待登记的处理器</param>
    internal void AddCommandHandler(List<BaseCommandHandler> commandHandlers, BaseCommandHandler handler)
    {
        if (String.IsNullOrEmpty(handler.Cmd))
        {
            throw new InvalidOperationException(L.T("Type {0} does not provide a Cmd value.", handler.GetType().FullName));
        }

        // 如果已经存在相同命令的处理程序，但基类不同，优先使用继承非基类的处理程序
        if (_commandHandlerDict.ContainsKey(handler.Cmd))
        {
            var oldHandler = _commandHandlerDict[handler.Cmd];
            if (handler.GetType().BaseType != typeof(BaseCommandHandler))
            {
                commandHandlers.Remove(oldHandler);
            }
            else
            {
                throw new InvalidOperationException(L.T("Types {0} and {1} provide the same Cmd value [{2}].", handler.GetType().FullName, oldHandler.GetType().FullName, handler.Cmd));
            }
        }
        _commandHandlerDict[handler.Cmd] = handler;
        commandHandlers.Add(handler);
    }

    /// <summary>
    /// 处理命令
    /// </summary>
    /// <param name="cmd">命令</param>
    /// <param name="args">参数</param>
    public Boolean Handle(String cmd, String[] args)
    {
        if (_commandHandlerDict.TryGetValue(cmd, out var handler))
        {
            handler.Process(args);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 根据快捷键处理命令
    /// </summary>
    /// <param name="key"></param>
    /// <param name="args"></param>
    public Boolean Handle(Char key, String[] args)
    {
        foreach (var commandHandler in _commandHandlerList)
        {
            if (commandHandler.ShortcutKey == key && commandHandler.IsShowMenu())
            {
                Handle(commandHandler.Cmd, args);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 获取快捷菜单信息
    /// </summary>
    /// <returns>快捷菜单集合</returns>
    public SortedSet<Menu> GetShortcutMenu()
    {
        var menus = new SortedSet<Menu>();
        foreach (var handler in _commandHandlerList)
        {
            if (handler.ShortcutKey != null && handler.IsShowMenu())
            {
                menus.Add(new Menu(handler.ShortcutKey.Value, handler.Description, handler.Cmd, null));
            }
        }
        return menus;
    }

    /// <summary>
    /// 获取所有命令处理程序
    /// </summary>
    /// <returns>所有命令处理程序列表</returns>
    public List<BaseCommandHandler> GetAllCommand() => _commandHandlerList;
}