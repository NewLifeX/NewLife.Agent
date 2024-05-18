using System.Reflection;

namespace NewLife.Agent.Command;

/// <summary>
/// 
/// </summary>
public class CommandFactory
{
    private readonly List<BaseCommandHandler> _commandHandlerList;
    private Dictionary<String, BaseCommandHandler> _commandHandlerDict = new Dictionary<String, BaseCommandHandler>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="service">服务对象</param>
    /// <param name="customCommandHandlerAssembly">自定义命令处理程序所在程序集</param>
    public CommandFactory(ServiceBase service, params Assembly[] customCommandHandlerAssembly)
    {
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

        // 使用反射获取所有实现了ICommandHandler接口的类型
        var commandHandlerTypes = assemblies.Values.SelectMany(n=>n.GetTypes().Where(t => typeof(BaseCommandHandler).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)).ToList();
         var commandHandlers = new List<BaseCommandHandler>();
        foreach (var type in commandHandlerTypes)
        {
            var handler = (BaseCommandHandler)Activator.CreateInstance(type, service);
            if (String.IsNullOrEmpty(handler.Cmd))
            {
                throw new InvalidOperationException($"类型 {type.FullName} 未提供命令 Cmd 值");
            }

            // 如果已经存在相同命令的处理程序，但基类不同，优先使用继承非基类的处理程序
            if (_commandHandlerDict.ContainsKey(handler.Cmd))
            {
                if (handler.GetType().BaseType != typeof(BaseCommandHandler))
                {
                    commandHandlers.Remove(_commandHandlerDict[handler.Cmd]);
                }
                else
                {
                    var oldHandler = _commandHandlerDict[handler.Cmd] = handler;
                    throw new InvalidOperationException($"类型 {type.FullName} 与 {oldHandler.GetType().FullName} 提供的命令 Cmd 值 [{handler.Cmd}] 相同");
                }
            }
            _commandHandlerDict[handler.Cmd] = handler;
            commandHandlers.Add(handler);
        }
        _commandHandlerList = commandHandlers.OrderBy(n => n.Cmd).ToList();
    }

    /// <summary>
    /// 处理命令
    /// </summary>
    /// <param name="cmd">命令</param>
    /// <param name="args">参数</param>
    public void Handle(String cmd, String[] args = null)
    {
        if (_commandHandlerDict.TryGetValue(cmd, out var handler))
        {
            handler.Process(args);
        }
        else
        {
            Console.WriteLine($"您输入的命令参数 [{cmd}] 无效，请重新输入！");
        }
    }

    /// <summary>
    /// 根据快捷键处理命令
    /// </summary>
    /// <param name="key"></param>
    /// <param name="args"></param>
    public void Handle(Char key, String[] args = null)
    {
        foreach (var commandHandler in _commandHandlerList)
        {
            if (commandHandler.ShortcutKey == key && commandHandler.IsShowMenu())
            {
                Handle(commandHandler.Cmd, args);
                return;
            }
        }
        Console.WriteLine($"您输入的命令序号 [{key}] 无效，请重新输入！");
    }

    /// <summary>
    /// 获取快捷菜单信息
    /// </summary>
    /// <returns></returns>
    public SortedSet<Menu> GetShortcutMenu()
    {
        var menus = new SortedSet<Menu>();
        foreach (var commandHandler in _commandHandlerList)
        {
            if (commandHandler.ShortcutKey != null && commandHandler.IsShowMenu())
            {
                menus.Add(new Menu(commandHandler.ShortcutKey.Value, commandHandler.Description, commandHandler.Cmd));
            }
        }
        return menus;
    }

    /// <summary>
    /// 获取所有命令处理程序
    /// </summary>
    /// <returns></returns>
    public List<BaseCommandHandler> GetAllCommand() => _commandHandlerList;
}