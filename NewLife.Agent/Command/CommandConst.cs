
namespace NewLife.Agent.Command;

/// <summary>
/// 基础命令常量
/// </summary>
public class CommandConst
{
    /// <summary>
    /// 显示状态命令
    /// </summary>
    public const String ShowStatus = "-status";

    /// <summary>
    /// 安装并启动服务命令
    /// </summary>
    public const String InstallAndStart = "-install";

    /// <summary>
    /// 安装服务命令
    /// </summary>
    public const String Install = "-i";

    /// <summary>
    /// 重新安装服务命令
    /// </summary>
    public const String Reinstall = "-reinstall";

    /// <summary>
    /// 卸载服务命令
    /// </summary>
    public const String Remove = "-u";

    /// <summary>
    /// 停止并卸载服务命令
    /// </summary>
    public const String Uninstall = "-uninstall";

    /// <summary>
    /// 执行服务命令
    /// </summary>
    public const String RunService = "-s";

    /// <summary>
    /// 模拟运行命令
    /// </summary>
    public const String RunSimulation = "-run";

    /// <summary>
    /// 启动服务命令
    /// </summary>
    public const String Start = "-start";

    /// <summary>
    /// 停止服务命令
    /// </summary>
    public const String Stop = "-stop";

    /// <summary>
    /// 重启服务命令
    /// </summary>
    public const String Restart = "-restart";

    /// <summary>
    /// 看门狗命令
    /// </summary>
    public const String WatchDog = "-watch";
}