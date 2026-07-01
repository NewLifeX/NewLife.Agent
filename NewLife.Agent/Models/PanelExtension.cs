namespace NewLife.Agent.WebPanel;

/// <summary>Web面板扩展信息，用户可通过继承AgentWebPanel并重写GetExtensions注册自定义面板</summary>
public class PanelExtension
{
    /// <summary>唯一标识</summary>
    public String Id { get; set; } = "";

    /// <summary>显示名称（导航Tab文字）</summary>
    public String Name { get; set; } = "";

    /// <summary>图标（Emoji或SVG）</summary>
    public String Icon { get; set; } = "";

    /// <summary>数据API端点路径，面板加载时前端请求此地址获取内容</summary>
    public String ApiEndpoint { get; set; } = "";

    /// <summary>排序（越小越靠前）</summary>
    public Int32 Order { get; set; }

    /// <summary>渲染模式：html-后端返回HTML片段直接插入，data-后端返回JSON数据前端渲染</summary>
    public String Mode { get; set; } = "data";
}
