namespace NewLife.Agent.Models;

/// <summary>服务模型</summary>
public class ServiceModel
{
    #region 属性
    /// <summary>服务名</summary>
    public String ServiceName { get; set; }

    /// <summary>中文名</summary>
    public String DisplayName { get; set; }

    /// <summary>描述</summary>
    public String Description { get; set; }

    /// <summary>文件名</summary>
    public String FileName { get; set; }

    /// <summary>参数</summary>
    public String Arguments { get; set; }

    /// <summary>工作目录</summary>
    public String WorkingDirectory { get; set; }

    /// <summary>用户</summary>
    public String User { get; set; }

    /// <summary>组</summary>
    public String Group { get; set; }
    #endregion
}
