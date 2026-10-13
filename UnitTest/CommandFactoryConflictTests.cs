using System.ComponentModel;
using Moq;
using NewLife.Agent;
using NewLife.Agent.Command;
using NewLife.Agent.CommandHandler;

namespace UnitTest;

/// <summary>命令工厂注册冲突单元测试</summary>
/// <remarks>覆盖 AddCommandHandler 的同命令冲突、空命令校验与派生覆盖语义</remarks>
public class CommandFactoryConflictTests
{
    #region 同命令直接子类冲突
    /// <summary>测试用冲突处理器</summary>
    /// <remarks>UnitTest 程序集会被命令工厂反射扫描并实例化，构造函数必须使用唯一 Cmd 以免污染其它测试；冲突命令仅在测试方法内构造后临时赋予</remarks>
    private sealed class ConflictHandler : BaseCommandHandler
    {
        /// <summary>实例化</summary>
        /// <param name="service">服务对象</param>
        public ConflictHandler(ServiceBase service) : base(service) => Cmd = "-utconflicttest";

        /// <inheritdoc/>
        public override void Process(String[] args) { }
    }

    [Fact]
    [DisplayName("同命令直接子类冲突_抛异常且消息含新旧类型名")]
    public void AddCommandHandler_SameCmd_DirectSubclass_Throws()
    {
        // 动态代理服务类型位于动态程序集，命令工厂不会扫描 UnitTest 程序集，得到仅含内建处理器的干净工厂
        var mb = new Mock<ServiceBase> { CallBase = true };
        var factory = new CommandFactory(mb.Object);
        var list = factory.GetAllCommand().ToList();
        var handler = new ConflictHandler(mb.Object) { Cmd = CommandConst.ShowStatus };

        var ex = Assert.Throws<InvalidOperationException>(() => factory.AddCommandHandler(list, handler));

        // 消息必须同时打印新类型与旧类型；曾出现旧类型位置重复打印新类型的缺陷
        Assert.Contains(typeof(ConflictHandler).FullName, ex.Message);
        Assert.Contains(typeof(ShowStatus).FullName, ex.Message);
        Assert.DoesNotContain(handler, list);
    }
    #endregion

    #region 空命令校验
    [Fact]
    [DisplayName("空命令_抛异常且消息含类型与Cmd字样")]
    public void AddCommandHandler_EmptyCmd_Throws()
    {
        var mb = new Mock<ServiceBase> { CallBase = true };
        var factory = new CommandFactory(mb.Object);
        var list = factory.GetAllCommand().ToList();
        var handler = new ConflictHandler(mb.Object) { Cmd = "" };

        var ex = Assert.Throws<InvalidOperationException>(() => factory.AddCommandHandler(list, handler));

        Assert.Contains(typeof(ConflictHandler).FullName, ex.Message);
        Assert.Contains("Cmd", ex.Message);
        Assert.DoesNotContain(handler, list);
    }
    #endregion

    #region 派生覆盖
    /// <summary>测试用安装命令派生处理器</summary>
    /// <remarks>UnitTest 程序集会被命令工厂反射扫描并实例化，构造函数必须使用唯一 Cmd；重写 Process 以避免真实安装副作用</remarks>
    private sealed class InstallOverride : Install
    {
        /// <summary>实例化</summary>
        /// <param name="service">服务对象</param>
        public InstallOverride(ServiceBase service) : base(service) => Cmd = "-utoverridetest";

        /// <summary>Process 是否已执行</summary>
        public Boolean Processed { get; private set; }

        /// <inheritdoc/>
        public override void Process(String[] args) => Processed = true;
    }

    [Fact]
    [DisplayName("派生处理器覆盖_替换旧登记且字典指向新实例")]
    public void AddCommandHandler_DerivedHandler_ReplacesBuiltin()
    {
        var mb = new Mock<ServiceBase> { CallBase = true };
        var factory = new CommandFactory(mb.Object);
        var handler = new InstallOverride(mb.Object) { Cmd = CommandConst.Install };

        // 构造期登记列表与 GetAllCommand 返回同一实例，直接传入以观察覆盖后的登记结果
        var list = factory.GetAllCommand();
        factory.AddCommandHandler(list, handler);

        Assert.Contains(factory.GetAllCommand(), e => e is InstallOverride);
        Assert.DoesNotContain(factory.GetAllCommand().OfType<Install>(), e => e is not InstallOverride);

        // 覆盖后字典指向新处理器，Handle 解析走字典
        Assert.True(factory.Handle(CommandConst.Install, null));
        Assert.True(handler.Processed);
    }
    #endregion
}
