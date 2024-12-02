﻿namespace NewLife.Agent.Models;

/// <summary>服务配置</summary>
public class ServiceConfig
{
    /// <summary>名称</summary>
    public String Name { get; set; }

    /// <summary>显示名</summary>
    public String DisplayName { get; set; }

    /// <summary>文件路径</summary>
    public String FilePath { get; set; }

    /// <summary>参数</summary>
    public String Arguments { get; set; }

    /// <summary>自动启动</summary>
    public Boolean AutoStart { get; set; }

    /// <summary>原始命令</summary>
    public String Command { get; set; }

    ///// <summary>描述</summary>
    //public String Description { get; set; }
}
