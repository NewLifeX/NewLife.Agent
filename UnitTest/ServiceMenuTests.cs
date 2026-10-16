using System.ComponentModel;
using NewLife.Agent;

namespace UnitTest;

/// <summary>交互菜单循环单元测试</summary>
/// <remarks>
/// 重写 ReadMenuKey 注入按键序列、重写 ShowMenu 抑制输出，覆盖菜单循环的命令分发、
/// 无效按键兜底与 '0' 退出分支；按键脚本耗尽即抛异常，用于判定循环是否按预期退出。
/// </remarks>
[Collection("WebPanel")]
public class ServiceMenuTests
{
    /// <summary>菜单循环测试子类。按键序列由构造参数注入</summary>
    private sealed class MenuTester : ServiceBase
    {
        private readonly Queue<ConsoleKeyInfo> _keys = new();

        public MenuTester(TestHost host, params Char[] keys)
        {
            Host = host;
            foreach (var key in keys)
            {
                _keys.Enqueue(new ConsoleKeyInfo(key, ConsoleKey.A, false, false, false));
            }
        }

        /// <summary>ShowMenu 是否被调用</summary>
        public Boolean MenuShown { get; private set; }

        /// <inheritdoc/>
        protected override void ShowMenu() => MenuShown = true;

        /// <inheritdoc/>
        protected override ConsoleKeyInfo ReadMenuKey()
        {
            if (_keys.Count == 0) throw new InvalidOperationException("按键脚本已耗尽，菜单循环未按预期退出");
            return _keys.Dequeue();
        }

        /// <summary>暴露菜单循环供测试驱动</summary>
        /// <param name="args">命令行参数</param>
        public void RunMenu(String[] args) => ProcessMenu(args);
    }

    [Fact]
    [DisplayName("菜单循环_无效按键后执行有效命令_0退出")]
    public void Menu_InvalidThenValidThenExit()
    {
        using var scope = new SettingScope();

        var host = new TestHost();
        var tester = new MenuTester(host, '9', '1', '0');

        tester.RunMenu(null);

        Assert.True(tester.MenuShown);
        // '1' 为“显示状态”快捷键，经 TestHost 观测命令已真实分发
        Assert.Contains(nameof(TestHost.IsInstalled), host.Calls);
    }

    [Fact]
    [DisplayName("菜单循环_输入0_不执行任何命令直接退出")]
    public void Menu_ExitImmediately()
    {
        using var scope = new SettingScope();

        var host = new TestHost();
        var tester = new MenuTester(host, '0');

        tester.RunMenu(null);

        Assert.True(tester.MenuShown);
        Assert.Empty(host.Calls);
    }

    [Fact]
    [DisplayName("菜单循环_回车键_忽略并继续读取")]
    public void Menu_NewlineKey_Ignored()
    {
        using var scope = new SettingScope();

        var host = new TestHost();
        var tester = new MenuTester(host, '\r', '\n', '0');

        tester.RunMenu(null);

        Assert.Empty(host.Calls);
    }

    [Fact]
    [DisplayName("菜单循环_连续多键_逐个处理直至退出")]
    public void Menu_MultipleKeys_ProcessedInOrder()
    {
        using var scope = new SettingScope();

        var host = new TestHost();
        var tester = new MenuTester(host, '2', '1', '0');

        tester.RunMenu(null);

        Assert.True(tester.MenuShown);
        // '2' 安装命令走 Install/IsInstalled，'1' 状态命令再次查询 IsInstalled，两个按键均被顺序处理
        Assert.Contains(nameof(TestHost.Install), host.Calls);
        Assert.Contains(nameof(TestHost.IsInstalled), host.Calls);
    }
}
