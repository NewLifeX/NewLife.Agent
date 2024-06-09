using System.Diagnostics;
using NewLife.Log;

namespace NewLife.Agent
{
    /// <summary>
    /// SysVinit版进程守护
    /// </summary>
    public class SysVinit : DefaultHost
    {
        public static readonly String ServicePath = "/etc/init.d";

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
            var status = Execute($"{ServicePath}/{serviceName}", "status", false);
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

            return Execute("update-rc.d", $"-f {serviceName} remove").Contains("Removing any system startup links for");
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

        private static String Execute(String cmd, String arguments, Boolean writeLog = true)
        {
            if (writeLog) XTrace.WriteLine("{0} {1}", cmd, arguments);

            var psi = new ProcessStartInfo(cmd, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            var process = Process.Start(psi);
            if (!process.WaitForExit(3_000))
            {
                process.Kill();
                return null;
            }

            return process.StandardOutput.ReadToEnd();
        }


        private String GetServicePath(String serviceName)
        {
            var file = Path.Combine(ServicePath, serviceName);
            return File.Exists(file) ? file : null;
        }
    }
}
