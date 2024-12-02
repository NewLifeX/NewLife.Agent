﻿using System.Diagnostics;
using NewLife.Agent.Models;
using NewLife.Log;

namespace NewLife.Agent;

/// <summary>SysVinit版进程守护</summary>
public class SysVinit : DefaultHost
{
    /// <summary>服务路径</summary>
    public static String ServicePath { get; set; }

    /// <summary>是否可用</summary>
    public static Boolean Available => !ServicePath.IsNullOrEmpty();

    /// <summary>相关目录</summary>
    private readonly static String[] _paths = [
        "/etc/init.d",
    ];

    static SysVinit()
    {
        foreach (var p in _paths)
        {
            if (Directory.Exists(p))
            {
                ServicePath = p;
                break;
            }
        }
    }

    /// <summary>实例化</summary>
    public SysVinit() => Name = "sysVinit";

    /// <summary>服务是否已安装</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override Boolean IsInstalled(String serviceName)
    {
        var file = GetServicePath(serviceName);
        return file != null;
    }

    /// <summary>服务是否已启动</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override Boolean IsRunning(String serviceName)
    {
        var file = GetServicePath(serviceName);
        if (file == null) return false;

        var status = $"{ServicePath}/{serviceName}".Execute("status", 3_000);
        return status.Contains("running");
    }

    /// <summary>安装服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <param name="displayName">显示名</param>
    /// <param name="fileName">文件路径</param>
    /// <param name="arguments">命令参数</param>
    /// <param name="description">描述信息</param>
    /// <returns></returns>
    public override Boolean Install(String serviceName, String displayName, String fileName, String arguments, String description)
    {
        //暂不实现SysVinit服务安装
        throw new NotImplementedException("SysVinit installation is not implemented.");
    }

    /// <summary>卸载服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override Boolean Remove(String serviceName)
    {
        XTrace.WriteLine("{0}.Remove {1}", Name, serviceName);

        var file = GetServicePath(serviceName);
        if (file == null) return false;

        return "update-rc.d".Execute($"-f {serviceName} remove", 3_000).Contains("Removing any system startup links for");
    }

    /// <summary>启动服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override Boolean Start(String serviceName)
    {
        XTrace.WriteLine("{0}.Start {1}", Name, serviceName);
        var file = GetServicePath(serviceName);
        if (file == null) return false;

        return Process.Start(file, "start") != null;
    }

    /// <summary>停止服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override Boolean Stop(String serviceName)
    {
        XTrace.WriteLine("{0}.Stop {1}", Name, serviceName);
        var file = GetServicePath(serviceName);
        if (file == null) return false;

        return Process.Start(file, "stop") != null;
    }

    /// <summary>重启服务</summary>
    /// <param name="serviceName">服务名</param>
    public override Boolean Restart(String serviceName)
    {
        XTrace.WriteLine("{0}.Restart {1}", Name, serviceName);
        var file = GetServicePath(serviceName);
        if (file == null) return false;

        return Process.Start(file, "restart") != null;
    }

    /// <summary>查询服务配置</summary>
    /// <param name="serviceName">服务名</param>
    public override ServiceConfig QueryConfig(String serviceName)
    {
        var file = GetServicePath(serviceName);
        if (file == null) return null;

        //  /etc/init.d 目录下的服务配置文件不是systemd格式，特殊处理
        // systemd-sysv-generator 在系统启动时自动运行，它会扫描 /etc/init.d 目录，
        // 并为每个脚本生成一个对应的 systemd 服务单元文件。
        // 这些生成的单元文件存放在 /run/systemd/generator.late/ 目录中。
        file = "/run/systemd/generator.late".CombinePath($"{serviceName}.service");
        if (!File.Exists(file))
        {
            return null;
        }

        var txt = File.ReadAllText(file);
        if (txt != null)
        {
            var dic = txt.SplitAsDictionary("=", "\n", true);

            var cfg = new ServiceConfig { Name = serviceName };
            if (dic.TryGetValue("ExecStart", out var str)) cfg.FilePath = str.Trim();
            if (dic.TryGetValue("WorkingDirectory", out str)) cfg.FilePath = str.Trim().CombinePath(cfg.FilePath);
            if (dic.TryGetValue("Description", out str)) cfg.DisplayName = str.Trim();
            if (dic.TryGetValue("Restart", out str)) cfg.AutoStart = !str.Trim().EqualIgnoreCase("no");

            return cfg;
        }

        return null;
    }

    /// <summary>
    /// 获取服务配置文件的路径
    /// </summary>
    /// <param name="serviceName">服务名称</param>
    /// <returns></returns>
    public static String GetServicePath(String serviceName)
    {
        var file = Path.Combine(ServicePath, serviceName);
        return File.Exists(file) ? file : null;
    }
}
